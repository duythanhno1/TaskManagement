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
        private readonly ILocalizer _localizer;

        public ChatService(ApplicationDbContext context, IHubContext<ChatHub> chatHubContext, INotificationService notificationService, ILocalizer localizer)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _notificationService = notificationService;
            _localizer = localizer;
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
                return ServiceResult<object>.BadRequest(_localizer.Get("chat.users_not_found"));
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

            var otherMemberIds = dto.MemberUserIds.Where(id => id != currentUserId).ToList();
            if (otherMemberIds.Count > 0)
            {
                await _notificationService.CreateBulkNotificationsAsync(
                    otherMemberIds, "GroupInvite",
                    "New Chat Group",
                    $"You have been added to group: {group.GroupName}",
                    group.GroupId.ToString());
            }

            return ServiceResult<object>.Created(new { group.GroupId, group.GroupName }, _localizer.Get("chat.group_created"));
        }

        public async Task<ServiceResult<List<object>>> GetMyGroupsAsync(int currentUserId)
        {
            var groups = await _context.ChatGroupMembers
                .Where(m => m.UserId == currentUserId)
                .AsSplitQuery()
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
                    UnreadCount = m.Group.Messages
                        .Count(msg => !msg.IsDeleted
                            && msg.SenderUserId != currentUserId
                            && !msg.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                })
                .ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(groups);
        }

        public async Task<ServiceResult<List<object>>> GetMessagesAsync(int currentUserId, int groupId, int? cursor = null, int pageSize = 50)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<List<object>>.BadRequest(_localizer.Get("chat.not_member"));
            }

            var query = _context.ChatMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted);

            if (cursor.HasValue)
            {
                query = query.Where(m => m.MessageId < cursor.Value);
            }

            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Take(pageSize + 1)
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
                    ReactionSummary = m.Reactions
                        .GroupBy(r => r.ReactionType)
                        .Select(g => new
                        {
                            Type = g.Key,
                            Count = g.Count(),
                            UserIds = g.Select(r => r.UserId)
                        }),
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

        public async Task<ServiceResult<object>> SendMessageAsync(int currentUserId, SendMessageDTO dto)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == dto.GroupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));
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

            return ServiceResult<object>.Created(messageData, _localizer.Get("chat.message_sent"));
        }

        public async Task<ServiceResult<object>> DeleteMessageAsync(int currentUserId, int messageId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null)
            {
                return ServiceResult<object>.NotFound(_localizer.Get("chat.message_not_found"));
            }

            if (message.SenderUserId != currentUserId)
            {
                return ServiceResult<object>.BadRequest(_localizer.Get("chat.delete_own_only"));
            }

            message.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.Group($"chat_{message.GroupId}")
                .SendAsync("MessageDeleted", new { messageId, message.GroupId });

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.message_deleted"));
        }

        public async Task<ServiceResult<object>> ReactToMessageAsync(int currentUserId, ReactMessageDTO dto)
        {
            var message = await _context.ChatMessages.FindAsync(dto.MessageId);
            if (message == null)
            {
                return ServiceResult<object>.NotFound(_localizer.Get("chat.message_not_found"));
            }

            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == message.GroupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));
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

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.reaction_added"));
        }

        public async Task<ServiceResult<object>> RemoveReactionAsync(int currentUserId, int messageId)
        {
            var reaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == currentUserId);

            if (reaction == null)
            {
                return ServiceResult<object>.NotFound(_localizer.Get("chat.reaction_not_found"));
            }

            var message = await _context.ChatMessages.FindAsync(messageId);

            _context.MessageReactions.Remove(reaction);
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.Group($"chat_{message.GroupId}")
                .SendAsync("ReactionRemoved", new { messageId, message.GroupId, currentUserId });

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.reaction_removed"));
        }

        public async Task<ServiceResult<object>> MarkAsReadAsync(int currentUserId, int groupId)
        {
            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);

            if (!isMember)
            {
                return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));
            }

            const int batchSize = 500;

            var unreadMessageIds = await _context.ChatMessages
                .Where(m => m.GroupId == groupId
                    && !m.IsDeleted
                    && m.SenderUserId != currentUserId
                    && !m.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                .OrderBy(m => m.MessageId)
                .Select(m => m.MessageId)
                .Take(batchSize)
                .ToListAsync();

            if (unreadMessageIds.Count == 0)
            {
                return ServiceResult<object>.Ok(
                    new { MarkedCount = 0, HasMore = false },
                    _localizer.Get("chat.all_read"));
            }

            var readStatuses = unreadMessageIds.Select(msgId => new MessageReadStatus
            {
                MessageId = msgId,
                UserId = currentUserId,
                ReadAt = DateTime.Now
            }).ToList();

            await _context.MessageReadStatuses.AddRangeAsync(readStatuses);
            await _context.SaveChangesAsync();

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
                _localizer.Get("chat.marked_read", unreadMessageIds.Count));
        }
    }
}
