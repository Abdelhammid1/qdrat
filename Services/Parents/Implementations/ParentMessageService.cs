using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentMessageService : IParentMessageService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IParentAccessService _accessService;

        public ParentMessageService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IParentAccessService accessService)
        {
            _contextFactory = contextFactory;
            _accessService = accessService;
        }

        public async Task<ParentMessagesListViewModel> GetMessagesAsync(string parentUserId, int? studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var parentId = await _accessService.GetCurrentParentIdAsync(parentUserId);
            if (parentId == null)
                return new ParentMessagesListViewModel();

            var query = db.Set<ParentMessage>()
                .AsNoTracking()
                .Where(m => m.ParentId == parentId);

            if (studentId.HasValue)
                query = query.Where(m => m.StudentId == studentId.Value);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id, m.Subject, m.MessageBody, m.Status,
                    m.CreatedAt, m.ReplyBody, m.RepliedAt, m.StudentId
                })
                .ToListAsync();

            var studentIds = messages.Select(m => m.StudentId).Distinct().ToList();
            var studentNames = await db.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.StudentID))
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();
            var nameDict = studentNames.ToDictionary(s => s.StudentID, s => s.FullName);

            var now = DateTime.Now;
            var vms = messages.Select(m => new ParentMessageViewModel
            {
                Id = m.Id,
                Subject = m.Subject,
                MessageBody = m.MessageBody,
                Status = m.Status,
                StatusLabel = GetStatusLabel(m.Status),
                StatusColor = GetStatusColor(m.Status),
                CreatedAt = m.CreatedAt,
                TimeAgo = GetTimeAgo(m.CreatedAt, now),
                HasReply = !string.IsNullOrEmpty(m.ReplyBody),
                ReplyBody = m.ReplyBody,
                RepliedAt = m.RepliedAt.HasValue ? m.RepliedAt.Value.ToString("yyyy/MM/dd HH:mm") : null,
                StudentName = nameDict.GetValueOrDefault(m.StudentId, "الطالب")
            }).ToList();

            return new ParentMessagesListViewModel
            {
                Messages = vms,
                TotalCount = vms.Count,
                UnrepliedCount = vms.Count(v => !v.HasReply)
            };
        }

        public async Task<ParentMessageViewModel?> GetMessageDetailsAsync(int messageId, int parentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var m = await db.Set<ParentMessage>()
                .AsNoTracking()
                .Where(x => x.Id == messageId && x.ParentId == parentId)
                .Select(x => new
                {
                    x.Id, x.Subject, x.MessageBody, x.Status,
                    x.CreatedAt, x.ReplyBody, x.RepliedAt, x.StudentId
                })
                .FirstOrDefaultAsync();

            if (m == null) return null;

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == m.StudentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            return new ParentMessageViewModel
            {
                Id = m.Id,
                Subject = m.Subject,
                MessageBody = m.MessageBody,
                Status = m.Status,
                StatusLabel = GetStatusLabel(m.Status),
                StatusColor = GetStatusColor(m.Status),
                CreatedAt = m.CreatedAt,
                TimeAgo = GetTimeAgo(m.CreatedAt, DateTime.Now),
                HasReply = !string.IsNullOrEmpty(m.ReplyBody),
                ReplyBody = m.ReplyBody,
                RepliedAt = m.RepliedAt.HasValue ? m.RepliedAt.Value.ToString("yyyy/MM/dd HH:mm") : null,
                StudentName = studentName
            };
        }

        public async Task CreateMessageAsync(string parentUserId, ParentMessageCreateViewModel model)
        {
            using var db = _contextFactory.CreateDbContext();

            var parentId = await _accessService.GetCurrentParentIdAsync(parentUserId);
            if (parentId == null) return;

            var msg = new ParentMessage
            {
                ParentId = parentId.Value,
                StudentId = model.StudentId,
                Subject = model.Subject,
                MessageBody = model.MessageBody,
                Status = "New",
                CreatedAt = DateTime.Now
            };

            db.Set<ParentMessage>().Add(msg);
            await db.SaveChangesAsync();
        }

        private static string GetStatusLabel(string status) => status switch
        {
            "New" => "جديدة",
            "Read" => "مقروءة",
            "Replied" => "تم الرد",
            "Closed" => "مغلقة",
            _ => status
        };

        private static string GetStatusColor(string status) => status switch
        {
            "New" => "primary",
            "Read" => "secondary",
            "Replied" => "success",
            "Closed" => "dark",
            _ => "secondary"
        };

        private static string GetTimeAgo(DateTime dt, DateTime now)
        {
            var diff = now - dt;
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            return $"منذ {(int)diff.TotalDays} يوم";
        }
    }
}
