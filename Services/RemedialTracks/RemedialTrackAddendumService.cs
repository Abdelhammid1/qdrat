using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S13: إدارة ملاحق المحاور. كل كتابة داخل Transaction واحدة (إلا InMemory في الاختبارات)؛ الإشعارات وسجل نشاط الأدمن
    /// بعد نجاحها وفشلهما لا يُبطل العملية (نفس نمط <see cref="RemedialTrackPublicationService"/>). الأوقات UTC.
    /// </summary>
    public sealed class RemedialTrackAddendumService : IRemedialTrackAddendumService
    {
        public const int MaxTargetEnrollmentIds = 500;
        private const int MinReasonLength = 5;
        private const int MaxReasonLength = 300;
        private const int MaxTitleLength = 200;
        private const int MinExamMinutes = 5;
        private const int MaxExamMinutes = 180;
        private const int MaxVideoSeconds = 86400;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly INotificationService _notifications;
        private readonly IAdminActivityLogger _activity;
        private readonly ILogger<RemedialTrackAddendumService> _logger;

        public RemedialTrackAddendumService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            INotificationService notifications,
            IAdminActivityLogger activity,
            ILogger<RemedialTrackAddendumService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _notifications = notifications;
            _activity = activity;
            _logger = logger;
        }

        // ======================================================================
        // إنشاء
        // ======================================================================

        public async Task<RemedialTrackResult> CreateAsync(
            CreateAddendumInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;

            // ── فحوص بلا قاعدة بيانات ──
            var title = input.Title?.Trim();
            if (string.IsNullOrEmpty(title))
                return RemedialTrackResult.Fail("⚠️ عنوان الفيديو مطلوب.");
            if (title.Length > MaxTitleLength)
                return RemedialTrackResult.Fail($"⚠️ عنوان الفيديو لا يتجاوز {MaxTitleLength} حرفًا.");

            var reason = input.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < MinReasonLength || reason.Length > MaxReasonLength)
                return RemedialTrackResult.Fail($"⚠️ سبب الإضافة مطلوب ({MinReasonLength}–{MaxReasonLength} حرف).");

            var parsed = RemedialTrackVideoUrlParser.Parse(input.Url);
            if (!parsed.IsValid)
                return RemedialTrackResult.Fail("⚠️ " + parsed.Error);
            if (parsed.RequiresDuration &&
                (input.DurationSeconds is null || input.DurationSeconds < RemedialTrackVideoUrlParser.MinOtherDurationSeconds))
                return RemedialTrackResult.Fail($"⚠️ مدة الفيديو (بالثواني) إلزامية لهذه المنصة ولا تقل عن {RemedialTrackVideoUrlParser.MinOtherDurationSeconds}.");
            if (input.DurationSeconds is < RemedialTrackVideoUrlParser.MinOtherDurationSeconds or > MaxVideoSeconds)
                return RemedialTrackResult.Fail("⚠️ مدة الفيديو بين 10 ثوانٍ و24 ساعة.");

            int? examMinutes = null;
            if (input.ExamModelId.HasValue)
            {
                if (input.ExamDurationMinutes is null or < MinExamMinutes or > MaxExamMinutes)
                    return RemedialTrackResult.Fail($"⚠️ مدة الاختبار بين {MinExamMinutes} و{MaxExamMinutes} دقيقة.");
                examMinutes = input.ExamDurationMinutes;
            }

            List<int>? ids = null;
            if (!input.ApplyToAll)
            {
                ids = (input.EnrollmentIds ?? Array.Empty<int>()).Distinct().ToList();
                if (ids.Count == 0)
                    return RemedialTrackResult.Fail("⚠️ اختر طالبًا واحدًا على الأقل أو طبّق على الجميع.");
                if (ids.Count > MaxTargetEnrollmentIds)
                    return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxTargetEnrollmentIds} طالب للطلب الواحد.");
            }

            var batchId = 0;
            var created = 0;
            var notifyStudentIds = new List<int>();
            var trackTitle = string.Empty;
            var addendumId = 0;

            RemedialTrackResult result;
            try
            {
                result = await WriteCoreAsync(async (db, token) =>
                {
                    var pub = await db.RemedialTrackPublications.AsNoTracking()
                        .Where(p => p.Id == input.PublicationId)
                        .Select(p => new
                        {
                            p.BatchId,
                            p.TrackId,
                            p.IsDeleted,
                            p.Status,
                            TrackTitle = p.Track!.Title,
                            p.Track.CurriculumId
                        })
                        .FirstOrDefaultAsync(token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (pub.IsDeleted || pub.Status != RemedialTrackPublicationStatus.Active)
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير نشط (ملغى أو محذوف).");
                    batchId = pub.BatchId;
                    trackTitle = pub.TrackTitle;

                    // المحور يجب أن يكون من خطة الأمر (لا محور خطة أخرى)
                    var axisOk = await db.RemedialTrackAxes.AsNoTracking()
                        .AnyAsync(a => a.Id == input.AxisId && a.TrackId == pub.TrackId, token);
                    if (!axisOk)
                        return RemedialTrackResult.Fail("⚠️ المحور ليس من خطة أمر النشر.");

                    // نموذج الاختبار (إن وُجد): نفس قواعد ValidateExamModelsAsync
                    var warnings = new List<string>();
                    if (input.ExamModelId.HasValue)
                    {
                        var modelId = input.ExamModelId.Value;
                        var model = await db.ProfessionalModels.AsNoTracking()
                            .Where(m => m.Id == modelId)
                            .Select(m => new { m.Title, m.IsArchived, m.ModelType, m.CurriculumId })
                            .FirstOrDefaultAsync(token);
                        if (model is null)
                            return RemedialTrackResult.Fail("⚠️ نموذج الاختبار غير موجود.");
                        if (model.IsArchived || model.ModelType != ProfessionalModelType.Exam)
                            return RemedialTrackResult.Fail($"⚠️ النموذج «{model.Title}» يجب أن يكون نموذج اختبار غير مؤرشف.");

                        var stat = await (from pmq in db.ProfessionalModelQuestions.AsNoTracking().Where(x => x.ModelId == modelId && x.QuestionId != null)
                                          join q in db.Questions.AsNoTracking() on pmq.QuestionId!.Value equals q.Id
                                          group q by pmq.ModelId into g
                                          select new { Total = g.Count(), Bad = g.Count(x => string.IsNullOrWhiteSpace(x.CorrectAnswer)) })
                            .FirstOrDefaultAsync(token);
                        if (stat is null || stat.Total == 0)
                            return RemedialTrackResult.Fail($"⚠️ النموذج «{model.Title}» لا يحتوي أسئلة.");
                        if (stat.Bad > 0)
                            return RemedialTrackResult.Fail($"⚠️ النموذج «{model.Title}» فيه {stat.Bad} سؤال بلا إجابة صحيحة (لن يعمل التصحيح).");
                        if (model.CurriculumId.HasValue && model.CurriculumId.Value != pub.CurriculumId)
                            warnings.Add($"النموذج «{model.Title}» تابع لمنهج مختلف عن منهج الخطة.");
                    }

                    // تسجيلات الأمر غير الملغاة — استعلام واحد (لا IN على قائمة): المعرّفات المطلوبة تُقاطَع في الذاكرة
                    var rows = await db.RemedialTrackEnrollments.AsNoTracking()
                        .Where(e => e.PublicationId == input.PublicationId && e.Status != RemedialTrackEnrollmentStatus.Cancelled)
                        .Select(e => new
                        {
                            e.Id,
                            e.StudentId,
                            AxisStatus = e.AxisProgresses.Where(a => a.AxisId == input.AxisId).Select(a => (RemedialTrackAxisStatus?)a.Status).FirstOrDefault()
                        })
                        .ToListAsync(token);

                    var targets = rows;
                    if (ids is not null)
                    {
                        var owned = rows.ToDictionary(r => r.Id);
                        if (ids.Any(id => !owned.ContainsKey(id)))
                            return RemedialTrackResult.Fail("🚫 بعض التسجيلات المحدّدة لا تتبع هذا الأمر أو ملغاة؛ لم يُنفَّذ شيء.");
                        targets = ids.Select(id => owned[id]).ToList();
                    }
                    if (targets.Count == 0)
                        return RemedialTrackResult.Fail("ℹ️ لا توجد تسجيلات نشطة لإضافة الملحق إليها.");

                    var addendum = new RemedialTrackAddendum
                    {
                        PublicationId = input.PublicationId,
                        AxisId = input.AxisId,
                        Title = title,
                        Url = parsed.NormalizedUrl!,
                        Provider = parsed.Provider,
                        ExternalId = parsed.ExternalId,
                        DurationSeconds = input.DurationSeconds,
                        ExamModelId = input.ExamModelId,
                        ExamDurationMinutes = examMinutes,
                        Reason = reason,
                        IsActive = true,
                        CreatedByUserId = Truncate(actor.UserId, 450),
                        CreatedByName = Truncate(actor.Name, 200),
                        CreatedAtUtc = now,
                        // صفوف تقدّم الطلاب دفعة واحدة (set-based): إدراج واحد بلا حفظ داخل حلقة
                        Progresses = targets.Select(t => new RemedialTrackAddendumProgress
                        {
                            EnrollmentId = t.Id,
                            DurationSeconds = input.DurationSeconds
                        }).ToList()
                    };
                    db.RemedialTrackAddenda.Add(addendum);
                    await db.SaveChangesAsync(token);

                    addendumId = addendum.Id;
                    created = targets.Count;
                    // المحور المقفل: يظهر له الملحق عند فتح المحور لا قبله (D31) فلا إشعار الآن
                    notifyStudentIds = targets
                        .Where(t => t.AxisStatus is not null && t.AxisStatus != RemedialTrackAxisStatus.Locked)
                        .Select(t => t.StudentId)
                        .Distinct()
                        .ToList();

                    return RemedialTrackResult.Ok($"✅ أُضيف الملحق لـ {created} طالبًا (لا يمنع تقدّمهم في المحاور).", addendumId,
                        warnings.Count == 0 ? null : warnings);
                }, ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK addendum create failed (publication {PublicationId})", input.PublicationId);
                return RemedialTrackResult.Fail("⚠️ تعذّر حفظ الملحق. أعد المحاولة.");
            }

            if (result.Success)
            {
                await NotifyAsync(notifyStudentIds, trackTitle, title);
                await LogActivityAsync("RemedialTrackAddendumCreated",
                    $"[RTK pub:{input.PublicationId}] إضافة ملحق «{title}» للمحور {input.AxisId} لـ {created} تسجيلًا ({(input.ApplyToAll ? "الكل" : "محدّدون")}) — السبب: {reason}"
                    + (input.ExamModelId.HasValue ? " — مع اختبار" : string.Empty),
                    actor, batchId);
            }
            return result;
        }

        // ======================================================================
        // إيقاف / تفعيل
        // ======================================================================

        public async Task<RemedialTrackResult> SetActiveAsync(
            int addendumId, bool active, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var batchId = 0;
            var publicationId = 0;
            var title = string.Empty;
            var changed = false;

            RemedialTrackResult result;
            try
            {
                result = await WriteCoreAsync(async (db, token) =>
                {
                    var addendum = await db.RemedialTrackAddenda.FirstOrDefaultAsync(a => a.Id == addendumId, token);
                    if (addendum is null)
                        return RemedialTrackResult.Fail("⚠️ الملحق غير موجود.");

                    var pubBatch = await db.RemedialTrackPublications.AsNoTracking()
                        .Where(p => p.Id == addendum.PublicationId)
                        .Select(p => (int?)p.BatchId)
                        .FirstOrDefaultAsync(token);
                    if (pubBatch is null || !scope.Allows(pubBatch.Value))
                        return RemedialTrackResult.Fail("⚠️ الملحق غير موجود.");

                    batchId = pubBatch.Value;
                    publicationId = addendum.PublicationId;
                    title = addendum.Title;

                    if (addendum.IsActive == active)
                        return RemedialTrackResult.Ok(active ? "ℹ️ الملحق مفعّل أصلًا." : "ℹ️ الملحق موقوف أصلًا.");   // Idempotent

                    addendum.IsActive = active;
                    await db.SaveChangesAsync(token);
                    changed = true;
                    return RemedialTrackResult.Ok(active ? "✅ فُعِّل الملحق." : "✅ أُوقف الملحق (يختفي عن الطلاب ولا يُحذف منه شيء).");
                }, ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK addendum toggle failed (addendum {AddendumId})", addendumId);
                return RemedialTrackResult.Fail("⚠️ تعذّر حفظ التغيير. أعد المحاولة.");
            }

            if (result.Success && changed)
                await LogActivityAsync("RemedialTrackAddendumToggled",
                    $"[RTK pub:{publicationId}] {(active ? "تفعيل" : "إيقاف")} الملحق «{title}» (#{addendumId})", actor, batchId);
            return result;
        }

        // ======================================================================
        // قراءة
        // ======================================================================

        private static IQueryable<AddendumTrackingRow> TrackingQuery(ApplicationDbContext db, int publicationId) =>
            db.RemedialTrackAddenda.AsNoTracking()
                .Where(a => a.PublicationId == publicationId)
                .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
                .Select(a => new AddendumTrackingRow
                {
                    AddendumId = a.Id,
                    AxisOrder = a.Axis!.Order,
                    AxisTitle = a.Axis.TitleOverride ?? a.Axis.Section!.Title,
                    Title = a.Title,
                    Reason = a.Reason,
                    IsActive = a.IsActive,
                    HasExam = a.ExamModelId != null,
                    CreatedByName = a.CreatedByName,
                    CreatedAtUtc = a.CreatedAtUtc,
                    // التجميع على التسجيلات غير الملغاة فقط
                    Targeted = a.Progresses.Count(p => p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled),
                    VideoCompleted = a.Progresses.Count(p => p.VideoCompleted && p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled),
                    ExamPassed = a.Progresses.Count(p => p.ExamPassed && p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled),
                    Completed = a.Progresses.Count(p => p.CompletedAtUtc != null && p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled),
                    AttemptsTotal = a.Progresses.Where(p => p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled).Sum(p => (int?)p.AttemptsCount) ?? 0,
                    LastActivityUtc = a.Progresses.Max(p => (DateTime?)p.LastPingAtUtc)
                });

        public async Task<IReadOnlyList<AddendumTrackingRow>> GetTrackingAsync(int publicationId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await TrackingQuery(db, publicationId).ToListAsync(ct);
        }

        public async Task<RemedialTrackAddendaPanelVm?> GetPanelAsync(
            int publicationId, int page, bool canManage, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var pub = await db.RemedialTrackPublications.AsNoTracking()
                .Where(p => p.Id == publicationId)
                .Select(p => new { p.BatchId, p.TrackId, p.IsDeleted, p.Status })
                .FirstOrDefaultAsync(ct);
            if (pub is null || !scope.Allows(pub.BatchId)) return null;

            var total = await db.RemedialTrackAddenda.AsNoTracking().CountAsync(a => a.PublicationId == publicationId, ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackAddendaPanelVm.PageSize);
            var safePage = Math.Clamp(page, 1, totalPages);

            var rows = await TrackingQuery(db, publicationId)
                .Skip((safePage - 1) * RemedialTrackAddendaPanelVm.PageSize)
                .Take(RemedialTrackAddendaPanelVm.PageSize)
                .ToListAsync(ct);

            var vm = new RemedialTrackAddendaPanelVm
            {
                CanManage = canManage,
                PublicationActive = !pub.IsDeleted && pub.Status == RemedialTrackPublicationStatus.Active,
                Rows = rows,
                Page = safePage,
                TotalPages = totalPages,
                Total = total
            };

            // خيارات نموذج الإنشاء تُحمَّل لمن يملك الصلاحية فقط وللأمر النشط فقط
            if (canManage && vm.PublicationActive)
            {
                var axes = await db.RemedialTrackAxes.AsNoTracking()
                    .Where(a => a.TrackId == pub.TrackId)
                    .OrderBy(a => a.Order)
                    .Select(a => new { a.Id, a.Order, Title = a.TitleOverride ?? a.Section!.Title })
                    .ToListAsync(ct);
                vm.Axes = axes.Select(a => new RemedialTrackSelectOption { Id = a.Id, Text = $"{a.Order}. {a.Title}" }).ToList();

                vm.ExamModels = await db.ProfessionalModels.AsNoTracking()
                    .Where(m => !m.IsArchived && m.ModelType == ProfessionalModelType.Exam)
                    .OrderBy(m => m.Title)
                    .Select(m => new RemedialTrackSelectOption { Id = m.Id, Text = m.Title, CurriculumId = m.CurriculumId })
                    .ToListAsync(ct);
            }
            return vm;
        }

        public async Task<AddendumStudentsPage?> GetStudentsAsync(int addendumId, int page, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var head = await db.RemedialTrackAddenda.AsNoTracking()
                .Where(a => a.Id == addendumId)
                .Select(a => new { a.Publication!.BatchId, a.DurationSeconds })
                .FirstOrDefaultAsync(ct);
            if (head is null || !scope.Allows(head.BatchId)) return null;

            var q = db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.AddendumId == addendumId && p.Enrollment!.Status != RemedialTrackEnrollmentStatus.Cancelled);

            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)AddendumStudentsPage.PageSize);
            var safePage = Math.Clamp(page, 1, totalPages);

            var raw = await q
                .OrderBy(p => p.Enrollment!.Student!.FullName).ThenBy(p => p.Id)
                .Skip((safePage - 1) * AddendumStudentsPage.PageSize)
                .Take(AddendumStudentsPage.PageSize)
                .Select(p => new
                {
                    p.EnrollmentId,
                    StudentName = p.Enrollment!.Student!.FullName,
                    p.WatchedSeconds,
                    Duration = head.DurationSeconds ?? p.DurationSeconds,
                    p.VideoCompleted,
                    p.ExamPassed,
                    p.BestScorePercent,
                    p.AttemptsCount,
                    p.LastPingAtUtc,
                    p.CompletedAtUtc
                })
                .ToListAsync(ct);

            return new AddendumStudentsPage
            {
                Page = safePage,
                TotalPages = totalPages,
                Total = total,
                Items = raw.Select(r => new AddendumStudentRow
                {
                    EnrollmentId = r.EnrollmentId,
                    StudentName = r.StudentName,
                    WatchedPercent = r.VideoCompleted ? 100
                        : r.Duration is > 0 ? (int)Math.Clamp(Math.Floor(r.WatchedSeconds * 100.0 / r.Duration.Value), 0, 99) : 0,
                    VideoCompleted = r.VideoCompleted,
                    ExamPassed = r.ExamPassed,
                    BestScorePercent = r.BestScorePercent,
                    AttemptsCount = r.AttemptsCount,
                    LastActivityUtc = r.CompletedAtUtc ?? r.LastPingAtUtc,
                    Completed = r.CompletedAtUtc.HasValue
                }).ToList()
            };
        }

        // ======================================================================
        // مساعدات
        // ======================================================================

        private async Task NotifyAsync(List<int> studentIds, string trackTitle, string addendumTitle)
        {
            if (studentIds.Count == 0) return;
            try
            {
                var message = $"📘 مطلوب إضافي في خطتك العلاجية «{trackTitle}»: {addendumTitle}. لا يمنع تقدّمك في المحاور.";
                await _notifications.SendToStudentsAsync(studentIds, message, NotificationCategory.Remedial, "/Students/RemedialTrack");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل إرسال إشعارات الملحق ({Count} طالب)", studentIds.Count);
            }
        }

        private async Task LogActivityAsync(string actionType, string description, RemedialTrackActor actor, int batchId)
        {
            try
            {
                await _activity.LogAsync(actionType, description, actor.UserId, actor.Name, batchId: batchId > 0 ? batchId : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل نشاط الأدمن ({Action})", actionType);
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        private async Task<RemedialTrackResult> WriteCoreAsync(
            Func<ApplicationDbContext, CancellationToken, Task<RemedialTrackResult>> body, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var strategy = db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear(); // أمان عند إعادة تشغيل الـ Strategy

                // مزوّد InMemory (الاختبارات) لا يدعم Transactions
                await using var tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;

                var result = await body(db, ct);
                if (result.Success && tx is not null)
                    await tx.CommitAsync(ct);
                return result; // الفشل: التخلص من tx بلا Commit = Rollback
            });
        }
    }
}
