using Auth.API.Protos;
using Chat.API.Data;
using Chat.API.Hubs;
using Chat.API.Models;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure;

namespace Chat.API.Services
{
    public interface IChatService
    {
        Task<ServiceResult<object>> CreateGroupAsync(int currentUserId, CreateGroupDTO dto);
        Task<ServiceResult<List<object>>> GetMyGroupsAsync(int currentUserId);
        Task<ServiceResult<List<object>>> GetMessagesAsync(int currentUserId, int groupId, int? cursor, int pageSize);
        Task<ServiceResult<object>> SendMessageAsync(int currentUserId, SendMessageDTO dto);
        Task<ServiceResult<object>> DeleteMessageAsync(int currentUserId, int messageId);
        Task<ServiceResult<object>> ReactToMessageAsync(int currentUserId, ReactMessageDTO dto);
        Task<ServiceResult<object>> RemoveReactionAsync(int currentUserId, int messageId);
        Task<ServiceResult<object>> MarkAsReadAsync(int currentUserId, int groupId);
    }

    public class ChatService : IChatService
    {
        private readonly ChatDbContext _context;
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly ILocalizer _localizer;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly AuthGrpc.AuthGrpcClient _authGrpc;

        public ChatService(ChatDbContext context, IHubContext<ChatHub> chatHub, ILocalizer localizer, IPublishEndpoint publishEndpoint, AuthGrpc.AuthGrpcClient authGrpc)
        {
            _context = context; _chatHub = chatHub; _localizer = localizer; _publishEndpoint = publishEndpoint; _authGrpc = authGrpc;
        }

        public async Task<ServiceResult<object>> CreateGroupAsync(int currentUserId, CreateGroupDTO dto)
        {
            if (!dto.MemberUserIds.Contains(currentUserId)) dto.MemberUserIds.Add(currentUserId);

            var group = new ChatGroup { GroupName = dto.GroupName, IsDirectMessage = dto.IsDirectMessage, CreatedByUserId = currentUserId, CreatedAt = DateTime.Now };
            await _context.ChatGroups.AddAsync(group);
            await _context.SaveChangesAsync();

            // Get user names via gRPC
            var userNames = new Dictionary<int, string>();
            try
            {
                var req = new GetUsersByIdsRequest();
                req.UserIds.AddRange(dto.MemberUserIds);
                var resp = await _authGrpc.GetUsersByIdsAsync(req);
                foreach (var u in resp.Users) userNames[u.UserId] = u.FullName;
            }
            catch { }

            var members = dto.MemberUserIds.Select(uid => new ChatGroupMember
            {
                GroupId = group.GroupId, UserId = uid,
                UserFullName = userNames.GetValueOrDefault(uid),
                JoinedAt = DateTime.Now
            }).ToList();

            await _context.ChatGroupMembers.AddRangeAsync(members);
            await _context.SaveChangesAsync();

            var otherIds = dto.MemberUserIds.Where(id => id != currentUserId).ToList();
            if (otherIds.Count > 0)
            {
                await _publishEndpoint.Publish(new GroupCreatedEvent
                {
                    GroupId = group.GroupId, GroupName = group.GroupName,
                    CreatedByUserId = currentUserId, MemberUserIds = otherIds, CreatedAt = DateTime.Now
                });
            }

            return ServiceResult<object>.Created(new { group.GroupId, group.GroupName }, _localizer.Get("chat.group_created"));
        }

        public async Task<ServiceResult<List<object>>> GetMyGroupsAsync(int currentUserId)
        {
            var groups = await _context.ChatGroupMembers.Where(m => m.UserId == currentUserId).AsSplitQuery()
                .Select(m => new
                {
                    m.Group.GroupId, m.Group.GroupName, m.Group.IsDirectMessage, m.Group.CreatedAt,
                    Members = m.Group.Members.Select(member => new { member.UserId, member.UserFullName }),
                    LastMessage = m.Group.Messages.Where(msg => !msg.IsDeleted).OrderByDescending(msg => msg.SentAt)
                        .Select(msg => new { msg.Content, msg.SentAt, msg.SenderUserName }).FirstOrDefault(),
                    UnreadCount = m.Group.Messages.Count(msg => !msg.IsDeleted && msg.SenderUserId != currentUserId
                        && !msg.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                }).ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(groups);
        }

        public async Task<ServiceResult<List<object>>> GetMessagesAsync(int currentUserId, int groupId, int? cursor, int pageSize)
        {
            var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);
            if (!isMember) return ServiceResult<List<object>>.BadRequest(_localizer.Get("chat.not_member"));

            var query = _context.ChatMessages.Where(m => m.GroupId == groupId && !m.IsDeleted);
            if (cursor.HasValue) query = query.Where(m => m.MessageId < cursor.Value);

            var messages = await query.OrderByDescending(m => m.SentAt).Take(pageSize + 1)
                .Select(m => new
                {
                    m.MessageId, m.Content, m.SentAt, Sender = new { m.SenderUserId, m.SenderUserName },
                    ReactionSummary = m.Reactions.GroupBy(r => r.ReactionType).Select(g => new { Type = g.Key, Count = g.Count() }),
                    ReadCount = m.ReadStatuses.Count()
                }).ToListAsync();

            var hasMore = messages.Count > pageSize;
            var result = hasMore ? messages.Take(pageSize).ToList() : messages;
            var nextCursor = result.Any() ? result.Last().MessageId : (int?)null;

            return ServiceResult<List<object>>.Ok(result.Cast<object>().ToList(), hasMore ? $"NextCursor:{nextCursor}" : null);
        }

        public async Task<ServiceResult<object>> SendMessageAsync(int currentUserId, SendMessageDTO dto)
        {
            var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == dto.GroupId && m.UserId == currentUserId);
            if (!isMember) return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));

            string? senderName = null;
            try { var u = await _authGrpc.GetUserInfoAsync(new GetUserInfoRequest { UserId = currentUserId }); if (u.Found) senderName = u.FullName; } catch { }

            var message = new ChatMessage { GroupId = dto.GroupId, SenderUserId = currentUserId, SenderUserName = senderName, Content = dto.Content, SentAt = DateTime.Now };
            await _context.ChatMessages.AddAsync(message);
            await _context.SaveChangesAsync();

            var msgData = new { message.MessageId, message.GroupId, message.Content, message.SentAt, Sender = new { UserId = currentUserId, FullName = senderName } };
            await _chatHub.Clients.Group($"chat_{dto.GroupId}").SendAsync("ReceiveMessage", msgData);

            var otherIds = await _context.ChatGroupMembers.Where(m => m.GroupId == dto.GroupId && m.UserId != currentUserId).Select(m => m.UserId).ToListAsync();
            if (otherIds.Count > 0)
            {
                var group = await _context.ChatGroups.FindAsync(dto.GroupId);
                await _publishEndpoint.Publish(new MessageSentEvent
                {
                    MessageId = message.MessageId, GroupId = dto.GroupId, GroupName = group?.GroupName ?? "",
                    SenderUserId = currentUserId, SenderUserName = senderName ?? "",
                    Content = dto.Content.Length > 100 ? dto.Content[..100] + "..." : dto.Content,
                    OtherMemberUserIds = otherIds, SentAt = DateTime.Now
                });
            }

            return ServiceResult<object>.Created(msgData, _localizer.Get("chat.message_sent"));
        }

        public async Task<ServiceResult<object>> DeleteMessageAsync(int currentUserId, int messageId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null) return ServiceResult<object>.NotFound(_localizer.Get("chat.message_not_found"));
            if (message.SenderUserId != currentUserId) return ServiceResult<object>.BadRequest(_localizer.Get("chat.delete_own_only"));

            message.IsDeleted = true;
            await _context.SaveChangesAsync();
            await _chatHub.Clients.Group($"chat_{message.GroupId}").SendAsync("MessageDeleted", new { messageId, message.GroupId });

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.message_deleted"));
        }

        public async Task<ServiceResult<object>> ReactToMessageAsync(int currentUserId, ReactMessageDTO dto)
        {
            var message = await _context.ChatMessages.FindAsync(dto.MessageId);
            if (message == null) return ServiceResult<object>.NotFound(_localizer.Get("chat.message_not_found"));

            var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == message.GroupId && m.UserId == currentUserId);
            if (!isMember) return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));

            var existing = await _context.MessageReactions.FirstOrDefaultAsync(r => r.MessageId == dto.MessageId && r.UserId == currentUserId);
            if (existing != null) { existing.ReactionType = dto.ReactionType; existing.CreatedAt = DateTime.Now; }
            else { await _context.MessageReactions.AddAsync(new MessageReaction { MessageId = dto.MessageId, UserId = currentUserId, ReactionType = dto.ReactionType, CreatedAt = DateTime.Now }); }

            await _context.SaveChangesAsync();

            string? userName = null;
            try { var u = await _authGrpc.GetUserInfoAsync(new GetUserInfoRequest { UserId = currentUserId }); if (u.Found) userName = u.FullName; } catch { }

            await _chatHub.Clients.Group($"chat_{message.GroupId}").SendAsync("ReceiveReaction", new { dto.MessageId, message.GroupId, dto.ReactionType, User = new { UserId = currentUserId, FullName = userName } });

            if (message.SenderUserId != currentUserId)
            {
                await _publishEndpoint.Publish(new ReactionAddedEvent
                {
                    MessageId = dto.MessageId, GroupId = message.GroupId, ReactedByUserId = currentUserId,
                    ReactedByUserName = userName ?? "", ReactionType = dto.ReactionType,
                    MessageOwnerUserId = message.SenderUserId, CreatedAt = DateTime.Now
                });
            }

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.reaction_added"));
        }

        public async Task<ServiceResult<object>> RemoveReactionAsync(int currentUserId, int messageId)
        {
            var reaction = await _context.MessageReactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == currentUserId);
            if (reaction == null) return ServiceResult<object>.NotFound(_localizer.Get("chat.reaction_not_found"));

            var message = await _context.ChatMessages.FindAsync(messageId);
            _context.MessageReactions.Remove(reaction);
            await _context.SaveChangesAsync();
            await _chatHub.Clients.Group($"chat_{message!.GroupId}").SendAsync("ReactionRemoved", new { messageId, message.GroupId, currentUserId });

            return ServiceResult<object>.Ok(null, _localizer.Get("chat.reaction_removed"));
        }

        public async Task<ServiceResult<object>> MarkAsReadAsync(int currentUserId, int groupId)
        {
            var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == currentUserId);
            if (!isMember) return ServiceResult<object>.BadRequest(_localizer.Get("chat.not_member"));

            var unreadIds = await _context.ChatMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted && m.SenderUserId != currentUserId && !m.ReadStatuses.Any(rs => rs.UserId == currentUserId))
                .OrderBy(m => m.MessageId).Select(m => m.MessageId).Take(500).ToListAsync();

            if (unreadIds.Count == 0) return ServiceResult<object>.Ok(new { MarkedCount = 0, HasMore = false }, _localizer.Get("chat.all_read"));

            var readStatuses = unreadIds.Select(id => new MessageReadStatus { MessageId = id, UserId = currentUserId, ReadAt = DateTime.Now }).ToList();
            await _context.MessageReadStatuses.AddRangeAsync(readStatuses);
            await _context.SaveChangesAsync();

            var hasMore = unreadIds.Count == 500;
            await _chatHub.Clients.Group($"chat_{groupId}").SendAsync("MessageRead", new { GroupId = groupId, UserId = currentUserId, ReadCount = unreadIds.Count, HasMore = hasMore });

            return ServiceResult<object>.Ok(new { MarkedCount = unreadIds.Count, HasMore = hasMore }, _localizer.Get("chat.marked_read", unreadIds.Count));
        }
    }

    public class CreateGroupDTO { public string GroupName { get; set; } = string.Empty; public bool IsDirectMessage { get; set; } public List<int> MemberUserIds { get; set; } = new(); }
    public class SendMessageDTO { public int GroupId { get; set; } public string Content { get; set; } = string.Empty; }
    public class ReactMessageDTO { public int MessageId { get; set; } public string ReactionType { get; set; } = string.Empty; }
}
