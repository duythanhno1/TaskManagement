using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Hubs;
using Managerment.Interfaces;
using Managerment.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Managerment.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly INotificationService _notificationService;

        public ChatService(ApplicationDbContext context, IHubContext<ChatHub> chatHubContext, INotificationService notificationService)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _notificationService = notificationService;
        }

        public async Task<ServiceResult<object>> CreateGroupAsync(int currentUserId, CreateGroupDTO dto)
        {
            if (!dto.MemberUserIds.Contains(currentUserId))
            {
                dto.MemberUserIds.Add(currentUserId);
            }

            var existingUserIds = await _context.Users
                .Where(u => dto.MemberUserIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .ToListAsync();

            if (existingUserIds.Count != dto.MemberUserIds.Count)
            {
                return ServiceResult<object>.BadRequest("One or more users not found.");
            }

            var group = new ChatGroup
            {
                GroupName = dto.GroupName,
                IsDirectMessage = dto.IsDirectMessage,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.Now
            };

            await _context.ChatGroups.AddAsync(group);
            await _context.SaveChangesAsync();

            var members = dto.MemberUserIds.Select(userId => new ChatGroupMember
            {
                GroupId = group.GroupId,
                UserId = userId,
                JoinedAt = DateTime.Now
            }).ToList();

            await _context.ChatGroupMembers.AddRangeAsync(members);
            await _context.SaveChangesAsync();

            // Batch notification cho tất cả members (trừ creator)
            var otherMemberIds = dto.MemberUserIds.Where(id => id != currentUserId).ToList();
            if (otherMemberIds.Count > 0)
            {
                await _notificationService.CreateBulkNotificationsAsync(
                    otherMemberIds, "GroupInvite",
                    "New Chat Group",
                    $"You have been added to group: {group.GroupName}",
                    group.GroupId.ToString());
            }

            return ServiceResult<object>.Created(new { group.GroupId, group.GroupName }, "Group created successfully.");
        }

        // FIX #1: AsSplitQuery + optimized projections để tránh cartesian explosion
        public async Task<ServiceResult<List<object>>> GetMyGroupsAsync(int currentUserId)
        {
            var groups = await _context.ChatGroupMembers
                .Where(m => m.UserId == currentUserId)
                .AsSplitQuery() // Tách thành nhiều SQL queries nhỏ thay vì 1 JOIN khổng lồ
                .Select(m => new
                {
                    m.Group.GroupId,
                    m.Group.GroupName,
                    m.Group.IsDirectMessage,
                    m.Group.CreatedAt,
                    Members = m.Group.Members.Select(member => new
                    {
                        member.User.UserId,
                        member.User.FullName
                    }),
                    // Sub-query cho LastMessage — EF Core dịch thành 1 subselect, không gây N+1
                    LastMessage = m.Group.Messages
                        .Where(msg => !msg.IsDeleted)
                        .OrderByDescending(msg => msg.SentAt)
                        .Select(msg => new
                        {
                            msg.Content,
                            msg.SentAt,
                            SenderName = msg.SenderUser.FullName
                        })
                        .FirstOrDefault(),
                    // Count chỉ trả số, không load entities
                    UnreadCount = m.Group.Messages
                        .Count(msg => !msg.IsDeleted
                            && msg.SenderUserId != currentUserId
                            && !msg.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                })
                .ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(groups);
        }

        // FIX #2 + #3: Cursor-based pagination + Reaction summary
        public async Task<ServiceResult<List<object>>> GetMessagesAsync(int currentUserId, int groupId, int? cursor = null, int pageSize = 50)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<List<object>>.BadRequest("You are not a member of this group.");
            }

            // Cursor-based: lấy messages có MessageId < cursor (mới nhất trước)
            var query = _context.ChatMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted);

            if (cursor.HasValue)
            {
                query = query.Where(m => m.MessageId < cursor.Value);
            }

            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Take(pageSize + 1) // Lấy thêm 1 để check HasMore
                .Select(m => new
                {
                    m.MessageId,
                    m.Content,
                    m.SentAt,
                    Sender = new
                    {
                        m.SenderUser.UserId,
                        m.SenderUser.FullName
                    },
                    // FIX #3: Group reactions thành summary thay vì trả full user info
                    ReactionSummary = m.Reactions
                        .GroupBy(r => r.ReactionType)
                        .Select(g => new
                        {
                            Type = g.Key,
                            Count = g.Count(),
                            UserIds = g.Select(r => r.UserId)
                        }),
                    // Chỉ trả ReadCount thay vì full ReadBy list
                    ReadCount = m.ReadStatuses.Count()
                })
                .ToListAsync();

            var hasMore = messages.Count > pageSize;
            var result = hasMore ? messages.Take(pageSize).ToList() : messages;
            var nextCursor = result.Any() ? result.Last().MessageId : (int?)null;

            return ServiceResult<List<object>>.Ok(
                result.Cast<object>().ToList(),
                hasMore ? $"NextCursor:{nextCursor}" : null);
        }

        // FIX #4: Batch notifications
        public async Task<ServiceResult<object>> SendMessageAsync(int currentUserId, SendMessageDTO dto)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == dto.GroupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest("You are not a member of this group.");
            }

            var sender = await _context.Users.FindAsync(currentUserId);

            var message = new ChatMessage
            {
                GroupId = dto.GroupId,
                SenderUserId = currentUserId,
                Content = dto.Content,
                SentAt = DateTime.Now
            };

            await _context.ChatMessages.AddAsync(message);
            await _context.SaveChangesAsync();

            var messageData = new
            {
                message.MessageId,
                message.GroupId,
                message.Content,
                message.SentAt,
                Sender = new
                {
                    sender.UserId,
                    sender.FullName
                }
            };

            await _chatHubContext.Clients.Group($"chat_{dto.GroupId}")
                .SendAsync("ReceiveMessage", messageData);

            // Batch notification — 1 lần INSERT + 1 lần SaveChanges thay vì N lần
            var otherMemberIds = await _context.ChatGroupMembers
                .Where(m => m.GroupId == dto.GroupId && m.UserId != currentUserId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (otherMemberIds.Count > 0)
            {
                var truncatedContent = dto.Content.Length > 100
                    ? dto.Content.Substring(0, 100) + "..."
                    : dto.Content;

                await _notificationService.CreateBulkNotificationsAsync(
                    otherMemberIds, "NewMessage",
                    $"New message from {sender.FullName}",
                    truncatedContent,
                    dto.GroupId.ToString());
            }

            return ServiceResult<object>.Created(messageData, "Message sent successfully.");
        }

        public async Task<ServiceResult<object>> DeleteMessageAsync(int currentUserId, int messageId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null)
            {
                return ServiceResult<object>.NotFound("Message not found.");
            }

            if (message.SenderUserId != currentUserId)
            {
                return ServiceResult<object>.BadRequest("You can only delete your own messages.");
            }

            message.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.Group($"chat_{message.GroupId}")
                .SendAsync("MessageDeleted", new { messageId, message.GroupId });

            return ServiceResult<object>.Ok(null, "Message deleted successfully.");
        }

        public async Task<ServiceResult<object>> ReactToMessageAsync(int currentUserId, ReactMessageDTO dto)
        {
            var message = await _context.ChatMessages.FindAsync(dto.MessageId);
            if (message == null)
            {
                return ServiceResult<object>.NotFound("Message not found.");
            }

            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == message.GroupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest("You are not a member of this group.");
            }

            var existingReaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == dto.MessageId && r.UserId == currentUserId);

            if (existingReaction != null)
            {
                existingReaction.ReactionType = dto.ReactionType;
                existingReaction.CreatedAt = DateTime.Now;
            }
            else
            {
                var reaction = new MessageReaction
                {
                    MessageId = dto.MessageId,
                    UserId = currentUserId,
                    ReactionType = dto.ReactionType,
                    CreatedAt = DateTime.Now
                };
                await _context.MessageReactions.AddAsync(reaction);
            }

            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(currentUserId);

            await _chatHubContext.Clients.Group($"chat_{message.GroupId}")
                .SendAsync("ReceiveReaction", new
                {
                    dto.MessageId,
                    message.GroupId,
                    dto.ReactionType,
                    User = new { user.UserId, user.FullName }
                });

            if (message.SenderUserId != currentUserId)
            {
                await _notificationService.CreateNotificationAsync(
                    message.SenderUserId, "Reaction",
                    $"{user.FullName} reacted {dto.ReactionType}",
                    $"Reacted to your message",
                    message.GroupId.ToString());
            }

            return ServiceResult<object>.Ok(null, "Reaction added successfully.");
        }

        public async Task<ServiceResult<object>> RemoveReactionAsync(int currentUserId, int messageId)
        {
            var reaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == currentUserId);

            if (reaction == null)
            {
                return ServiceResult<object>.NotFound("Reaction not found.");
            }

            var message = await _context.ChatMessages.FindAsync(messageId);

            _context.MessageReactions.Remove(reaction);
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.Group($"chat_{message.GroupId}")
                .SendAsync("ReactionRemoved", new { messageId, message.GroupId, currentUserId });

            return ServiceResult<object>.Ok(null, "Reaction removed.");
        }

        // FIX #5: Batch update với giới hạn 500 per batch
        public async Task<ServiceResult<object>> MarkAsReadAsync(int currentUserId, int groupId)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest("You are not a member of this group.");
            }

            const int batchSize = 500;

            // Chỉ lấy tối đa batchSize IDs, tránh load quá nhiều vào memory
            var unreadMessageIds = await _context.ChatMessages
                .Where(m => m.GroupId == groupId
                    && !m.IsDeleted
                    && m.SenderUserId != currentUserId
                    && !m.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                .OrderBy(m => m.MessageId) // Đọc từ cũ đến mới
                .Select(m => m.MessageId)
                .Take(batchSize)
                .ToListAsync();

            if (unreadMessageIds.Count == 0)
            {
                return ServiceResult<object>.Ok(
                    new { MarkedCount = 0, HasMore = false },
                    "All messages already read.");
            }

            var readStatuses = unreadMessageIds.Select(msgId => new MessageReadStatus
            {
                MessageId = msgId,
                UserId = currentUserId,
                ReadAt = DateTime.Now
            }).ToList();

            await _context.MessageReadStatuses.AddRangeAsync(readStatuses);
            await _context.SaveChangesAsync();

            // Kiểm tra còn unread messages không
            var hasMore = unreadMessageIds.Count == batchSize;

            await _chatHubContext.Clients.Group($"chat_{groupId}")
                .SendAsync("MessageRead", new
                {
                    GroupId = groupId,
                    UserId = currentUserId,
                    ReadCount = unreadMessageIds.Count,
                    HasMore = hasMore
                });

            return ServiceResult<object>.Ok(
                new { MarkedCount = unreadMessageIds.Count, HasMore = hasMore },
                $"{unreadMessageIds.Count} messages marked as read.");
        }
    }
}
