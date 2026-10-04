using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed class QuestionReviewTaskAdminQueryService : IQuestionReviewTaskAdminQueryService
    {
        private const int MaxPageSize = 100;
        private const int TimelineItemEvents = 30;
        private const int DashboardWindowDays = 90;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public QuestionReviewTaskAdminQueryService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }

        public static string TaskStatusLabel(QuestionReviewTaskStatus s) => s switch
        {
            QuestionReviewTaskStatus.Assigned => "مُسندة",
            QuestionReviewTaskStatus.InProgress => "قيد المراجعة",
            QuestionReviewTaskStatus.Completed => "مكتملة",
            QuestionReviewTaskStatus.Closed => "مغلقة",
            QuestionReviewTaskStatus.Cancelled => "ملغاة",
            _ => string.Empty
        };

        // ---------------------------------------------------------------- Index

        public async Task<AdminTasksIndexVm> GetIndexAsync(CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            // مجموع واحد على جدول المهام (عدادات مخزّنة) — المهام الملغاة خارج المؤشرات
            var k = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Status != QuestionReviewTaskStatus.Cancelled)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Open = g.Count(t => t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress),
                    Locked = g.Sum(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress) ? t.PendingItems : 0),
                    Overdue = g.Count(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress)
                                           && t.DueAtUtc != null && t.DueAtUtc < nowUtc),
                    Returned = g.Sum(t => t.ReturnedItems),
                    Total = g.Sum(t => t.TotalItems),
                    Removed = g.Sum(t => t.RemovedItems),
                    Approved = g.Sum(t => t.ApprovedItems)
                })
                .FirstOrDefaultAsync(ct);

            var kpis = k is null
                ? new AdminTasksKpisVm()
                : new AdminTasksKpisVm
                {
                    OpenTasks = k.Open,
                    LockedPending = k.Locked,
                    OverdueTasks = k.Overdue,
                    UnresolvedReturns = k.Returned,
                    AverageApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(
                        k.Approved, QuestionReviewTaskMetrics.Effective(k.Total, k.Removed))
                };

            var instructors = await db.QuestionReviewTasks.AsNoTracking()
                .Select(t => new { t.InstructorId, Name = t.Instructor!.FullName })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var curriculums = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.CurriculumId != null)
                .Select(t => new { Id = t.CurriculumId!.Value, Title = t.Curriculum!.Title })
                .Distinct()
                .OrderBy(x => x.Title)
                .ToListAsync(ct);

            var perInstructor = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Status != QuestionReviewTaskStatus.Cancelled)
                .GroupBy(t => new { t.InstructorId, Name = t.Instructor!.FullName })
                .Select(g => new
                {
                    g.Key.InstructorId,
                    g.Key.Name,
                    Tasks = g.Count(),
                    Pending = g.Sum(t => t.PendingItems),
                    Approved = g.Sum(t => t.ApprovedItems),
                    Returned = g.Sum(t => t.ReturnedItems),
                    Total = g.Sum(t => t.TotalItems),
                    Removed = g.Sum(t => t.RemovedItems)
                })
                .ToListAsync(ct);

            return new AdminTasksIndexVm
            {
                Kpis = kpis,
                Instructors = instructors.Select(i => new AdminFilterOptionVm { Id = i.InstructorId, Text = i.Name ?? "—" }).ToList(),
                Curriculums = curriculums.Select(c => new AdminFilterOptionVm { Id = c.Id, Text = c.Title ?? "—" }).ToList(),
                ByInstructor = perInstructor
                    .Select(x =>
                    {
                        var effective = QuestionReviewTaskMetrics.Effective(x.Total, x.Removed);
                        return new AdminInstructorApprovalVm
                        {
                            InstructorId = x.InstructorId,
                            InstructorName = x.Name ?? "—",
                            Tasks = x.Tasks,
                            Pending = x.Pending,
                            Approved = x.Approved,
                            Returned = x.Returned,
                            Effective = effective,
                            ApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(x.Approved, effective)
                        };
                    })
                    .OrderByDescending(x => x.Pending)
                    .ThenBy(x => x.InstructorName)
                    .ToList()
            };
        }

        public async Task<AdminTasksPage> GetTasksPageAsync(
            AdminTasksFilter filter, int start, int length, int orderColumn, bool orderDescending, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            start = Math.Max(start, 0);
            length = length <= 0 ? 25 : Math.Min(length, MaxPageSize);

            var total = await db.QuestionReviewTasks.AsNoTracking().CountAsync(ct);

            var query = db.QuestionReviewTasks.AsNoTracking().AsQueryable();

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(t => t.Status == status);
            }
            if (filter.InstructorId.HasValue)
            {
                var instructorId = filter.InstructorId.Value;
                query = query.Where(t => t.InstructorId == instructorId);
            }
            if (filter.CurriculumId.HasValue)
            {
                var curriculumId = filter.CurriculumId.Value;
                query = query.Where(t => t.CurriculumId == curriculumId);
            }
            if (filter.Priority.HasValue)
            {
                var priority = filter.Priority.Value;
                query = query.Where(t => t.Priority == priority);
            }
            if (filter.OverdueOnly)
            {
                query = query.Where(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress)
                                         && t.DueAtUtc != null && t.DueAtUtc < nowUtc);
            }

            var term = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            if (term is not null)
                query = query.Where(t => t.Code.Contains(term) || t.Title.Contains(term));

            var filtered = await query.CountAsync(ct);

            // ترتيب بقائمة بيضاء: 0 الكود، 3 الأسئلة، 6 المرتجع، 7 الموعد، 8 الحالة؛ الافتراضي الأحدث أولًا
            IOrderedQueryable<QdratNew.Entities.QuestionReviewTask> ordered = orderColumn switch
            {
                0 => orderDescending ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
                3 => orderDescending ? query.OrderByDescending(t => t.TotalItems) : query.OrderBy(t => t.TotalItems),
                6 => orderDescending ? query.OrderByDescending(t => t.ReturnedItems) : query.OrderBy(t => t.ReturnedItems),
                7 => orderDescending ? query.OrderByDescending(t => t.DueAtUtc) : query.OrderBy(t => t.DueAtUtc),
                8 => orderDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                _ => query.OrderByDescending(t => t.Id)
            };
            if (orderColumn is 0 or 3 or 6 or 7 or 8)
                ordered = ordered.ThenByDescending(t => t.Id);   // ترتيب ثابت عند تساوي القيم

            var rows = await ordered
                .Skip(start)
                .Take(length)
                .Select(t => new
                {
                    t.Id, t.Code, t.Title, t.Status, t.Priority, t.DueAtUtc,
                    Instructor = t.Instructor!.FullName,
                    Curriculum = t.Curriculum != null ? t.Curriculum.Title : null,
                    t.TotalItems, t.PendingItems, t.ApprovedItems, t.ReturnedItems, t.RemovedItems
                })
                .ToListAsync(ct);

            return new AdminTasksPage
            {
                Total = total,
                Filtered = filtered,
                Rows = rows.Select(t =>
                {
                    var effective = QuestionReviewTaskMetrics.Effective(t.TotalItems, t.RemovedItems);
                    return new AdminTaskRowDto
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Title = t.Title,
                        InstructorName = t.Instructor ?? "—",
                        CurriculumTitle = t.Curriculum,
                        Status = (int)t.Status,
                        StatusLabel = TaskStatusLabel(t.Status),
                        Priority = (int)t.Priority,
                        DueLocal = QuestionReviewTaskMetrics.FormatLocal(t.DueAtUtc),
                        IsOverdue = QuestionReviewTaskMetrics.IsOverdue(t.Status, t.DueAtUtc, nowUtc),
                        Total = t.TotalItems,
                        Effective = effective,
                        Pending = t.PendingItems,
                        Approved = t.ApprovedItems,
                        Returned = t.ReturnedItems,
                        ApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(t.ApprovedItems, effective),
                        HandledPercent = QuestionReviewTaskMetrics.HandledPercent(t.PendingItems, effective)
                    };
                }).ToList()
            };
        }

        // -------------------------------------------------------------- Details

        public async Task<AdminTaskDetailsVm?> GetDetailsAsync(int taskId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            var t = await db.QuestionReviewTasks.AsNoTracking()
                .Where(x => x.Id == taskId)
                .Select(x => new
                {
                    x.Id, x.Code, x.Title, x.AdminNote, x.Status, x.Priority, x.DueAtUtc,
                    Instructor = x.Instructor!.FullName,
                    Curriculum = x.Curriculum != null ? x.Curriculum.Title : null,
                    ParentCode = x.ParentTask != null ? x.ParentTask.Code : null,
                    x.CreatedByName, x.CreatedAtUtc, x.StartedAtUtc, x.CompletedAtUtc, x.ClosedAtUtc,
                    x.CancelledAtUtc, x.CancelReason, x.LastReminderAtUtc,
                    x.TotalItems, x.PendingItems, x.ApprovedItems, x.ReturnedItems, x.RemovedItems
                })
                .FirstOrDefaultAsync(ct);

            if (t is null)
                return null;

            var itemEvents = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.TaskId == taskId && i.ActionAtUtc != null)
                .OrderByDescending(i => i.ActionAtUtc)
                .Take(TimelineItemEvents)
                .Select(i => new { At = i.ActionAtUtc!.Value, i.Status, i.ActionByName, i.ReferenceNumberSnapshot, i.ReturnNote })
                .ToListAsync(ct);

            var events = new List<AdminTimelineEventVm>();

            void Add(DateTime? at, string icon, string tone, string text, string? detail = null)
            {
                if (!at.HasValue) return;
                var utc = DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);
                events.Add(new AdminTimelineEventVm
                {
                    AtUtc = utc,
                    AtLocal = QuestionReviewTaskMetrics.FormatLocal(utc) ?? string.Empty,
                    Icon = icon,
                    Tone = tone,
                    Text = text,
                    Detail = detail
                });
            }

            Add(t.CreatedAtUtc, "fa-paper-plane", "brand",
                "أُنشئت المهمة وأُسندت للمدرب", string.IsNullOrWhiteSpace(t.CreatedByName) ? null : "بواسطة " + t.CreatedByName);
            Add(t.StartedAtUtc, "fa-play", "warn", "بدأ المدرب المراجعة");
            Add(t.CompletedAtUtc, "fa-circle-check", "ok", "اكتملت المراجعة (لا أسئلة معلّقة)");
            Add(t.ClosedAtUtc, "fa-lock", "muted", "أُغلقت المهمة");
            Add(t.CancelledAtUtc, "fa-ban", "danger", "أُلغيت المهمة", t.CancelReason);
            Add(t.LastReminderAtUtc, "fa-bell", "accent", "أُرسل تذكير للمدرب");

            foreach (var e in itemEvents)
            {
                var reference = string.IsNullOrWhiteSpace(e.ReferenceNumberSnapshot) ? "" : " " + e.ReferenceNumberSnapshot;
                var by = string.IsNullOrWhiteSpace(e.ActionByName) ? null : "بواسطة " + e.ActionByName;
                switch (e.Status)
                {
                    case QuestionReviewTaskItemStatus.Approved:
                        Add(e.At, "fa-check", "ok", "اعتماد السؤال" + reference, by); break;
                    case QuestionReviewTaskItemStatus.EditedAndApproved:
                        Add(e.At, "fa-pen", "ok", "تعديل واعتماد السؤال" + reference, by); break;
                    case QuestionReviewTaskItemStatus.Returned:
                        Add(e.At, "fa-reply", "accent", "إرجاع السؤال" + reference + " للإدارة",
                            string.IsNullOrWhiteSpace(e.ReturnNote) ? by : e.ReturnNote); break;
                    case QuestionReviewTaskItemStatus.ApprovedByAdmin:
                        Add(e.At, "fa-user-shield", "ok", "اعتمدت الإدارة السؤال" + reference, by); break;
                    case QuestionReviewTaskItemStatus.Removed:
                        Add(e.At, "fa-trash-can", "muted", "أُزيل السؤال" + reference + " من المهمة", by); break;
                    case QuestionReviewTaskItemStatus.ReturnResolved:
                        Add(e.At, "fa-clipboard-check", "brand", "عولج إرجاع السؤال" + reference, by); break;
                }
            }

            var effective = QuestionReviewTaskMetrics.Effective(t.TotalItems, t.RemovedItems);

            return new AdminTaskDetailsVm
            {
                Id = t.Id,
                Code = t.Code,
                Title = t.Title,
                AdminNote = t.AdminNote,
                InstructorName = t.Instructor ?? "—",
                CurriculumTitle = t.Curriculum,
                ParentTaskCode = t.ParentCode,
                CreatedByName = t.CreatedByName,
                Status = t.Status,
                Priority = t.Priority,
                DueLocal = QuestionReviewTaskMetrics.FormatLocal(t.DueAtUtc),
                CreatedLocal = QuestionReviewTaskMetrics.FormatLocal(t.CreatedAtUtc),
                CancelReason = t.CancelReason,
                IsOverdue = QuestionReviewTaskMetrics.IsOverdue(t.Status, t.DueAtUtc, nowUtc),
                Progress = new ReviewTaskProgressVm
                {
                    Total = t.TotalItems,
                    Pending = t.PendingItems,
                    Approved = t.ApprovedItems,
                    Returned = t.ReturnedItems,
                    Removed = t.RemovedItems,
                    Effective = effective,
                    ApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(t.ApprovedItems, effective),
                    HandledPercent = QuestionReviewTaskMetrics.HandledPercent(t.PendingItems, effective)
                },
                Timeline = events.OrderByDescending(e => e.AtUtc).ToList()
            };
        }

        public async Task<AdminItemsPage?> GetItemsPageAsync(
            int taskId, int start, int length, int? statusFilter, string? search, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            if (!await db.QuestionReviewTasks.AsNoTracking().AnyAsync(t => t.Id == taskId, ct))
                return null;

            start = Math.Max(start, 0);
            length = length <= 0 ? 25 : Math.Min(length, MaxPageSize);

            var baseQuery = db.QuestionReviewTaskItems.AsNoTracking().Where(i => i.TaskId == taskId);
            var total = await baseQuery.CountAsync(ct);

            var query = baseQuery;
            if (statusFilter.HasValue)
            {
                var wanted = (QuestionReviewTaskItemStatus)statusFilter.Value;
                query = query.Where(i => i.Status == wanted);
            }

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (term is not null)
            {
                query = query.Where(i =>
                    (i.Question!.Title != null && i.Question.Title.Contains(term)) ||
                    (i.ReferenceNumberSnapshot != null && i.ReferenceNumberSnapshot.Contains(term)));
            }

            var filtered = ReferenceEquals(query, baseQuery) ? total : await query.CountAsync(ct);

            var rows = await query
                .OrderBy(i => i.SortOrder)
                .Skip(start)
                .Take(length)
                .Select(i => new
                {
                    i.Id,
                    i.QuestionId,
                    i.Status,
                    i.ReturnNote,
                    i.AdminResolutionNote,
                    i.ActionByName,
                    i.ActionAtUtc,
                    i.ReferenceNumberSnapshot,
                    Title = i.Question!.Title,
                    Section = i.Question.Section != null ? i.Question.Section.Title : null,
                    Lesson = i.Question.Lesson != null ? i.Question.Lesson.Title : null
                })
                .ToListAsync(ct);

            return new AdminItemsPage
            {
                Total = total,
                Filtered = filtered,
                Rows = rows.Select(r => new AdminItemRowDto
                {
                    ItemId = r.Id,
                    QuestionId = r.QuestionId,
                    Reference = string.IsNullOrWhiteSpace(r.ReferenceNumberSnapshot) ? "—" : r.ReferenceNumberSnapshot!,
                    Title = r.Title ?? string.Empty,
                    Section = r.Section ?? "—",
                    Lesson = r.Lesson ?? "—",
                    Status = (int)r.Status,
                    StatusLabel = QuestionReviewTaskQueryService.ItemStatusLabel(r.Status),
                    ActionBy = r.ActionByName,
                    ActionLocal = QuestionReviewTaskMetrics.FormatLocal(r.ActionAtUtc),
                    ReturnNote = r.ReturnNote,
                    AdminResolutionNote = r.AdminResolutionNote
                }).ToList()
            };
        }

        // ------------------------------------------------------------ Dashboard

        public async Task<ReviewTasksSummaryVm> GetDashboardSummaryAsync(CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var sinceUtc = nowUtc.AddDays(-DashboardWindowDays);

            // استعلام واحد: المفتوحة (أي تاريخ) + ما أُنشئ خلال 90 يومًا (للنسبة)، دون الملغاة
            var s = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Status != QuestionReviewTaskStatus.Cancelled
                            && (t.Status == QuestionReviewTaskStatus.Assigned
                                || t.Status == QuestionReviewTaskStatus.InProgress
                                || t.CreatedAtUtc >= sinceUtc))
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Open = g.Count(t => t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress),
                    Locked = g.Sum(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress) ? t.PendingItems : 0),
                    Overdue = g.Count(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress)
                                           && t.DueAtUtc != null && t.DueAtUtc < nowUtc),
                    Approved = g.Sum(t => t.CreatedAtUtc >= sinceUtc ? t.ApprovedItems : 0),
                    Total = g.Sum(t => t.CreatedAtUtc >= sinceUtc ? t.TotalItems : 0),
                    Removed = g.Sum(t => t.CreatedAtUtc >= sinceUtc ? t.RemovedItems : 0)
                })
                .FirstOrDefaultAsync(ct);

            if (s is null)
                return new ReviewTasksSummaryVm();

            return new ReviewTasksSummaryVm
            {
                OpenTasks = s.Open,
                LockedPending = s.Locked,
                Overdue = s.Overdue,
                ApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(
                    s.Approved, QuestionReviewTaskMetrics.Effective(s.Total, s.Removed))
            };
        }
    }
}
