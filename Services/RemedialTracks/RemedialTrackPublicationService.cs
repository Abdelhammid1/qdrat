using System.Globalization;
using Microsoft.Data.SqlClient;
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
    /// RTK-S3: نشر الخطة العلاجية (أمر النشر) — الاستهداف، التسجيلات، الرقم المرجعي، التجديد، الإلغاء.
    /// كل كتابة داخل Transaction واحدة (إلا مزوّد InMemory في الاختبارات). الإشعارات وسجل نشاط الأدمن بعد نجاح الـ Transaction
    /// وفشلهما لا يُبطل العملية. الأوقات UTC (D11).
    /// </summary>
    public sealed class RemedialTrackPublicationService : IRemedialTrackPublicationService
    {
        public const int MaxStudentsPerPublication = RemedialTrackPublishFormVm.MaxStudentsPerPublication;
        public const int MaxCodeRetries = RemedialTrackCodeGenerator.MaxCodeRetries;
        private const int CodeDrawsPerAttempt = 20;
        private const int MinCancelReasonLength = 5;
        private const int MaxCancelReasonLength = 300;
        private static readonly TimeSpan MaxPastPublishSkew = TimeSpan.FromHours(1);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IRemedialTrackCodeGenerator _codes;
        private readonly ITimeZoneService _tz;
        private readonly INotificationService _notifications;
        private readonly IAdminActivityLogger _activity;
        private readonly ILogger<RemedialTrackPublicationService> _logger;

        public RemedialTrackPublicationService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IRemedialTrackCodeGenerator codes,
            ITimeZoneService tz,
            INotificationService notifications,
            IAdminActivityLogger activity,
            ILogger<RemedialTrackPublicationService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _codes = codes;
            _tz = tz;
            _notifications = notifications;
            _activity = activity;
            _logger = logger;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        // ======================================================================
        // قراءة
        // ======================================================================

        public async Task<RemedialTrackPublicationIndexVm> GetIndexAsync(int page, RemedialTrackBatchScope scope, CancellationToken ct = default, bool deleted = false)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var q = db.RemedialTrackPublications.AsNoTracking().Where(p => p.IsDeleted == deleted);   // RTK v2/D26
            if (scope.PermittedBatchIds is { } permitted)
            {
                // قائمة صغيرة (دفعات الموظف) — CompatibilityLevel(120) يولّد IN بثوابت لا OPENJSON (نفس BatchesController).
                var ids = permitted.ToList();
                q = q.Where(p => ids.Contains(p.BatchId));
            }

            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackPublicationIndexVm.PageSize);
            var current = Math.Clamp(page, 1, totalPages);

            var items = await q
                .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
                .Skip((current - 1) * RemedialTrackPublicationIndexVm.PageSize)
                .Take(RemedialTrackPublicationIndexVm.PageSize)
                .Select(p => new RemedialTrackPublicationListItemVm
                {
                    Id = p.Id,
                    TrackId = p.TrackId,
                    TrackCode = p.Track!.Code,
                    TrackTitle = p.Track.Title,
                    BatchName = p.Batch!.Name,
                    Mode = p.Mode,
                    Scope = p.Scope,
                    PublishAtUtc = p.PublishAtUtc,
                    TotalStudents = p.TotalStudents,
                    Status = p.Status,
                    CreatedAtUtc = p.CreatedAtUtc,
                    DeletedAtUtc = p.DeletedAtUtc,
                    DeletedByName = p.DeletedByName,
                    DeleteReason = p.DeleteReason
                })
                .ToListAsync(ct);

            return new RemedialTrackPublicationIndexVm { Items = items, Page = current, Total = total, ShowingDeleted = deleted };
        }

        public async Task<RemedialTrackPublishFormVm> GetPublishFormAsync(int? trackId, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var tracks = await db.RemedialTracks.AsNoTracking()
                .Where(t => t.Status == RemedialTrackStatus.Ready)
                .OrderByDescending(t => t.CreatedAtUtc)
                .Select(t => new RemedialTrackSelectOption { Id = t.Id, Text = t.Code + " — " + t.Title })
                .ToListAsync(ct);

            var batches = (await db.Batches.AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new RemedialTrackSelectOption { Id = b.Id, Text = b.Name })
                    .ToListAsync(ct))
                .Where(b => scope.Allows(b.Id))
                .ToList();

            var vm = new RemedialTrackPublishFormVm { Tracks = tracks, Batches = batches };
            if (trackId is > 0 && tracks.Any(t => t.Id == trackId))
                vm.Input.TrackId = trackId.Value;
            return vm;
        }

        public async Task<RemedialTrackBatchStudentsPageVm?> GetStudentsOfBatchAsync(
            int batchId, string? search, int page, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            if (!scope.Allows(batchId))
                return null;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var batchOk = await db.Batches.AsNoTracking().AnyAsync(b => b.Id == batchId && !b.IsDeleted && !b.IsArchived, ct);
            if (!batchOk)
                return null;

            var q = from e in db.StudentBatchEnrollments.AsNoTracking()
                    join s in db.Students.AsNoTracking() on e.StudentID equals s.StudentID
                    where e.BatchId == batchId
                    select new { s.StudentID, s.FullName };

            var term = search?.Trim();
            if (!string.IsNullOrEmpty(term))
                q = q.Where(x => x.FullName.Contains(term));

            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackBatchStudentsPageVm.PageSize);
            var current = Math.Clamp(page, 1, totalPages);

            var items = await q
                .OrderBy(x => x.FullName).ThenBy(x => x.StudentID)
                .Skip((current - 1) * RemedialTrackBatchStudentsPageVm.PageSize)
                .Take(RemedialTrackBatchStudentsPageVm.PageSize)
                .Select(x => new RemedialTrackBatchStudentVm { Id = x.StudentID, Name = x.FullName })
                .ToListAsync(ct);

            return new RemedialTrackBatchStudentsPageVm { Items = items, Page = current, Total = total };
        }

        public async Task<RemedialTrackPublicationDetailsVm?> GetDetailsAsync(
            int id, bool includeAccessCode, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var vm = await db.RemedialTrackPublications.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new RemedialTrackPublicationDetailsVm
                {
                    Id = p.Id,
                    TrackId = p.TrackId,
                    TrackCode = p.Track!.Code,
                    TrackTitle = p.Track.Title,
                    CurriculumTitle = p.Track.Curriculum!.Title,
                    BatchId = p.BatchId,
                    BatchName = p.Batch!.Name,
                    Scope = p.Scope,
                    Mode = p.Mode,
                    Status = p.Status,
                    PublishAtUtc = p.PublishAtUtc,
                    CreatedAtUtc = p.CreatedAtUtc,
                    CreatedByName = p.CreatedByName,
                    AdminNote = p.AdminNote,
                    AutoSendParentReports = p.AutoSendParentReports,
                    TotalStudents = p.TotalStudents,
                    CancelledAtUtc = p.CancelledAtUtc,
                    CancelReason = p.CancelReason,
                    IsDeleted = p.IsDeleted,
                    DeletedAtUtc = p.DeletedAtUtc,
                    DeletedByName = p.DeletedByName,
                    DeleteReason = p.DeleteReason,
                    CodeVersion = p.CodeVersion,
                    CodeGeneratedAtUtc = p.CodeGeneratedAtUtc,
                    AccessCode = includeAccessCode && p.Mode == RemedialTrackDeliveryMode.InPerson ? p.AccessCode : null
                })
                .FirstOrDefaultAsync(ct);

            if (vm is null || !scope.Allows(vm.BatchId))
                return null;

            vm.StatusCounts = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.PublicationId == id)
                .GroupBy(e => e.Status)
                .Select(g => new RemedialTrackPublicationStatusCountVm { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var students = await (
                    from e in db.RemedialTrackEnrollments.AsNoTracking()
                    join s in db.Students.AsNoTracking() on e.StudentId equals s.StudentID
                    where e.PublicationId == id
                    orderby s.FullName, s.StudentID
                    select new RemedialTrackPublicationStudentVm
                    {
                        StudentId = s.StudentID,
                        StudentName = s.FullName,
                        Status = e.Status
                    })
                .Take(RemedialTrackPublicationDetailsVm.MaxStudentsListed + 1)
                .ToListAsync(ct);

            vm.StudentsTruncated = students.Count > RemedialTrackPublicationDetailsVm.MaxStudentsListed;
            vm.Students = students.Take(RemedialTrackPublicationDetailsVm.MaxStudentsListed).ToList();
            vm.RecentChanges = await GetRecentChangesAsync(db, id, vm.TrackId, ct);
            return vm;
        }

        /// <summary>
        /// RTK-S9.3: AdminActivityLog لا يحمل حقلًا يربطه بالخطة/الأمر، فيُربط بوسم ثابت في الوصف:
        /// [RTK pub:{id}] لإجراءات أمر النشر، و[RTK track:{id}] لتعديلات الخطة بعد النشر (تؤثّر على كل أوامرها).
        /// استعلام واحد مرقّم (≤ 20) مقيَّد مسبقًا بـ ActionType يبدأ بـ RemedialTrack.
        /// </summary>
        private static async Task<List<RemedialTrackRecentChangeVm>> GetRecentChangesAsync(
            ApplicationDbContext db, int publicationId, int trackId, CancellationToken ct)
        {
            var pubTag = $"[RTK pub:{publicationId}]";
            var trackTag = $"[RTK track:{trackId}]";

            var rows = await db.AdminActivityLogs.AsNoTracking()
                .Where(l => l.ActionType.StartsWith("RemedialTrack")
                            && (l.Description.Contains(pubTag) || l.Description.Contains(trackTag)))
                .OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id)
                .Take(RemedialTrackPublicationDetailsVm.MaxRecentChanges)
                .Select(l => new { l.ActionType, l.Description, l.AdminName, l.Timestamp })
                .ToListAsync(ct);

            return rows.Select(r => new RemedialTrackRecentChangeVm
            {
                ActionType = r.ActionType,
                ActionLabel = RecentChangeLabel(r.ActionType),
                Description = r.Description.Replace(pubTag, string.Empty).Replace(trackTag, string.Empty).Trim(),
                AdminName = r.AdminName,
                Timestamp = r.Timestamp
            }).ToList();
        }

        private static string RecentChangeLabel(string actionType) => actionType switch
        {
            "RemedialTrack.Publish" => "نشر الخطة",
            "RemedialTrack.Cancel" => "إلغاء أمر النشر",
            "RemedialTrack.RegenerateCode" => "تجديد الرقم المرجعي",
            "RemedialTrack.UnlockNext" => "فتح المحور التالي",
            "RemedialTrackPublicationDeleted" => "حذف أمر النشر",
            "RemedialTrackPublicationRestored" => "استرجاع أمر النشر",
            "RemedialTrackVideoEdited" => "تعديل فيديو",
            "RemedialTrackAxisExamsEdited" => "تعديل نموذجي الاختبار",
            "RemedialTrackVideoReviewChanged" => "وضع مراجعة الفيديوهات",
            "RemedialTrackAddendumCreated" => "إضافة ملحق",
            "RemedialTrackAddendumToggled" => "تفعيل/إيقاف ملحق",
            "RemedialTrackParentReportSent" => "إرسال تقرير ولي الأمر",
            _ => "إجراء"
        };

        // ======================================================================
        // النشر
        // ======================================================================

        public async Task<RemedialTrackResult> CreateAsync(
            CreateRemedialTrackPublicationInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(typeof(RemedialTrackPublicationScope), input.Scope))
                return RemedialTrackResult.Fail("⚠️ نطاق الاستهداف غير صالح.");
            if (!Enum.IsDefined(typeof(RemedialTrackDeliveryMode), input.Mode))
                return RemedialTrackResult.Fail("⚠️ نمط التقديم غير صالح.");
            if (!scope.Allows(input.BatchId))
                return RemedialTrackResult.Fail("🚫 لا تملك صلاحية النشر على هذه الدفعة.");

            var now = Now;
            var publishAtUtc = input.PublishAtLocal.HasValue
                ? _tz.ConvertToUtc(DateTime.SpecifyKind(input.PublishAtLocal.Value, DateTimeKind.Unspecified))
                : now;
            if (publishAtUtc < now - MaxPastPublishSkew)
                return RemedialTrackResult.Fail("⚠️ وقت النشر أقدم من الوقت الحالي بأكثر من ساعة.");

            var note = string.IsNullOrWhiteSpace(input.AdminNote) ? null : Truncate(input.AdminNote.Trim(), 500);

            var enrolledIds = new List<int>();
            var trackTitle = string.Empty;
            var inPerson = input.Mode == RemedialTrackDeliveryMode.InPerson;

            RemedialTrackResult result;
            try
            {
                result = await WriteWithCodeRetryAsync(inPerson, async (db, token) =>
                {
                    enrolledIds.Clear();

                    // 1) الخطة
                    var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == input.TrackId, token);
                    if (track is null)
                        return RemedialTrackResult.Fail("⚠️ الخطة غير موجودة.");
                    if (track.Status != RemedialTrackStatus.Ready)
                        return RemedialTrackResult.Fail("⚠️ لا تُنشر إلا خطة بحالة «جاهزة». جهّز الخطة أولًا من صفحة البناء.");
                    trackTitle = track.Title;

                    var axes = await db.RemedialTrackAxes.AsNoTracking()
                        .Where(a => a.TrackId == track.Id)
                        .OrderBy(a => a.Order)
                        .Select(a => new { a.Id, a.Order })
                        .ToListAsync(token);
                    if (axes.Count == 0)
                        return RemedialTrackResult.Fail("⚠️ الخطة بلا محاور.");

                    // 2) الدفعة
                    var batchOk = await db.Batches.AsNoTracking()
                        .AnyAsync(b => b.Id == input.BatchId && !b.IsDeleted && !b.IsArchived, token);
                    if (!batchOk)
                        return RemedialTrackResult.Fail("⚠️ الدفعة غير موجودة أو مؤرشفة.");

                    // 3) الطلاب المستهدفون (تقاطع في الذاكرة بـ HashSet — لا Contains على قائمة داخل EF)
                    var batchStudentIds = await db.StudentBatchEnrollments.AsNoTracking()
                        .Where(e => e.BatchId == input.BatchId)
                        .Select(e => e.StudentID)
                        .Distinct()
                        .ToListAsync(token);
                    var batchSet = batchStudentIds.ToHashSet();

                    List<int> targets;
                    if (input.Scope == RemedialTrackPublicationScope.WholeBatch)
                    {
                        targets = batchStudentIds;
                    }
                    else
                    {
                        var requested = (input.StudentIds ?? new List<int>()).Distinct().ToList();
                        if (requested.Count == 0)
                            return RemedialTrackResult.Fail("⚠️ اختر طالبًا واحدًا على الأقل.");
                        if (requested.Count > MaxStudentsPerPublication)
                            return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxStudentsPerPublication} طالب لأمر النشر الواحد.");

                        var outside = requested.Count(id => !batchSet.Contains(id));
                        if (outside > 0)
                            return RemedialTrackResult.Fail($"⚠️ {outside} من الطلاب المختارين ليسوا من هذه الدفعة.");
                        targets = requested;
                    }

                    if (targets.Count == 0)
                        return RemedialTrackResult.Fail("⚠️ لا يوجد طلاب في هذه الدفعة.");

                    // 4) التكرار: تسجيل نشط في نفس الخطة (Join واحد على طلاب الدفعة)
                    var duplicateIds = (await (
                            from en in db.RemedialTrackEnrollments.AsNoTracking()
                            join sbe in db.StudentBatchEnrollments.AsNoTracking() on en.StudentId equals sbe.StudentID
                            where en.TrackId == track.Id
                                  && sbe.BatchId == input.BatchId
                                  && (en.Status == RemedialTrackEnrollmentStatus.NotStarted
                                      || en.Status == RemedialTrackEnrollmentStatus.InProgress)
                            select en.StudentId)
                        .Distinct()
                        .ToListAsync(token))
                        .ToHashSet();

                    var toEnroll = targets.Where(id => !duplicateIds.Contains(id)).OrderBy(id => id).ToList();
                    var skipped = targets.Count - toEnroll.Count;

                    if (toEnroll.Count == 0)
                        return RemedialTrackResult.Fail($"⚠️ كل الطلاب المستهدفين ({skipped}) لديهم هذه الخطة قيد التنفيذ؛ لا جديد للنشر.");
                    if (toEnroll.Count > MaxStudentsPerPublication)
                        return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxStudentsPerPublication} طالب لأمر النشر الواحد (المستهدف {toEnroll.Count}). اختر طلابًا محددين.");

                    // 5) الرقم المرجعي (حضوري)
                    string? code = null;
                    if (inPerson)
                    {
                        code = await NewUniqueCodeAsync(db, token);
                        if (code is null)
                            return RemedialTrackResult.Fail("⚠️ تعذّر توليد رقم مرجعي فريد. أعد المحاولة.");
                    }

                    // 6) رسم الكائنات الكامل ثم SaveChanges واحد (D17)
                    var pub = new RemedialTrackPublication
                    {
                        TrackId = track.Id,
                        BatchId = input.BatchId,
                        Scope = input.Scope,
                        Mode = input.Mode,
                        PublishAtUtc = publishAtUtc,
                        Status = RemedialTrackPublicationStatus.Active,
                        AccessCode = code,
                        CodeVersion = 1,
                        CodeGeneratedAtUtc = code is null ? null : now,
                        AdminNote = note,
                        AutoSendParentReports = input.AutoSendParentReports,
                        TotalStudents = toEnroll.Count,
                        CreatedByUserId = actor.UserId,
                        CreatedByName = Truncate(actor.Name, 200),
                        CreatedAtUtc = now
                    };

                    var firstAxisId = axes[0].Id;
                    var enrollMessage = Truncate($"تم تسجيل الطالب في الخطة العلاجية «{track.Title}» ({track.Code}).", 500);

                    foreach (var studentId in toEnroll)
                    {
                        var enrollment = new RemedialTrackEnrollment
                        {
                            TrackId = track.Id,
                            StudentId = studentId,
                            Status = RemedialTrackEnrollmentStatus.NotStarted,
                            CurrentAxisId = firstAxisId,
                            CreatedAtUtc = now
                        };

                        for (var i = 0; i < axes.Count; i++)
                        {
                            var first = i == 0;
                            enrollment.AxisProgresses.Add(new RemedialTrackAxisProgress
                            {
                                AxisId = axes[i].Id,
                                Order = axes[i].Order,
                                Status = first ? RemedialTrackAxisStatus.Videos : RemedialTrackAxisStatus.Locked,
                                Round = 1,
                                OpenedAtUtc = first ? publishAtUtc : null
                            });
                        }

                        enrollment.Events.Add(new RemedialTrackEvent
                        {
                            Type = RemedialTrackEventType.StudentEnrolled,
                            Message = enrollMessage,
                            ActorUserId = actor.UserId,
                            ActorName = Truncate(actor.Name, 200),
                            CreatedAtUtc = now
                        });

                        pub.Enrollments.Add(enrollment);
                    }

                    db.RemedialTrackPublications.Add(pub);

                    // 7) قفل بنية الخطة (D12)
                    if (!track.IsStructureLocked)
                    {
                        track.IsStructureLocked = true;
                        track.UpdatedAtUtc = now;
                    }

                    await db.SaveChangesAsync(token);

                    enrolledIds.AddRange(toEnroll);
                    return RemedialTrackResult.Ok(
                        skipped > 0
                            ? $"✅ تم النشر لـ {toEnroll.Count} طالبًا. تم تخطي {skipped} طالبًا لديهم الخطة نفسها قيد التنفيذ."
                            : $"✅ تم النشر لـ {toEnroll.Count} طالبًا.",
                        new RemedialTrackPublicationCreated(pub.Id, toEnroll.Count, skipped));
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّلت الخطة من مستخدم آخر أثناء النشر. أعد المحاولة.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK publish failed (track {TrackId}, batch {BatchId})", input.TrackId, input.BatchId);
                return RemedialTrackResult.Fail("⚠️ تعذّر حفظ أمر النشر بسبب تعارض في البيانات. أعد المحاولة.");
            }

            if (!result.Success || result.Data is not RemedialTrackPublicationCreated created)
                return result;

            // ما بعد الـ Transaction: لا يُبطل فشلها النشر
            await NotifyStudentsAsync(enrolledIds, trackTitle, publishAtUtc, input.Mode);
            await LogActivityAsync(
                "RemedialTrack.Publish",
                $"[RTK pub:{created.PublicationId}] نشر الخطة العلاجية «{trackTitle}» (أمر نشر #{created.PublicationId}) — {created.Enrolled} طالب، {(inPerson ? "حضوري" : "أونلاين")}",
                actor, input.BatchId);

            return result;
        }

        // ======================================================================
        // الرقم المرجعي والإلغاء
        // ======================================================================

        public async Task<RemedialTrackResult> RegenerateCodeAsync(
            int id, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var batchId = 0;
            RemedialTrackResult result;
            try
            {
                result = await WriteWithCodeRetryAsync(true, async (db, token) =>
                {
                    var pub = await db.RemedialTrackPublications.FirstOrDefaultAsync(p => p.Id == id, token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (pub.Mode != RemedialTrackDeliveryMode.InPerson)
                        return RemedialTrackResult.Fail("⚠️ الرقم المرجعي خاص بأوامر النشر الحضورية فقط.");
                    if (pub.IsDeleted || pub.Status != RemedialTrackPublicationStatus.Active)
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير نشط.");

                    var code = await NewUniqueCodeAsync(db, token);
                    if (code is null)
                        return RemedialTrackResult.Fail("⚠️ تعذّر توليد رقم مرجعي فريد. أعد المحاولة.");

                    pub.AccessCode = code;
                    pub.CodeVersion++;
                    pub.CodeGeneratedAtUtc = Now;
                    batchId = pub.BatchId;

                    await db.SaveChangesAsync(token);
                    return RemedialTrackResult.Ok("✅ تم توليد رقم مرجعي جديد. سيُطلب من الطلاب إدخاله عند فتح محور أو فيديو أو بدء اختبار.", code);
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّل أمر النشر من مستخدم آخر. حدّث الصفحة وأعد المحاولة.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK regenerate code failed (publication {PublicationId})", id);
                return RemedialTrackResult.Fail("⚠️ تعذّر حفظ الرقم الجديد. أعد المحاولة.");
            }

            if (result.Success)
                await LogActivityAsync("RemedialTrack.RegenerateCode", $"[RTK pub:{id}] تجديد الرقم المرجعي لأمر النشر #{id}", actor, batchId);
            return result;
        }

        public async Task<RemedialTrackResult> CancelAsync(
            int id, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason) || cleanReason.Length < MinCancelReasonLength || cleanReason.Length > MaxCancelReasonLength)
                return RemedialTrackResult.Fail($"⚠️ سبب الإلغاء إلزامي ({MinCancelReasonLength}–{MaxCancelReasonLength} حرفًا).");

            var batchId = 0;
            RemedialTrackResult result;
            try
            {
                result = await WriteCoreAsync(async (db, token) =>
                {
                    var pub = await db.RemedialTrackPublications.FirstOrDefaultAsync(p => p.Id == id, token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (pub.IsDeleted || pub.Status != RemedialTrackPublicationStatus.Active)
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير نشط أو ملغى سابقًا.");

                    var now = Now;
                    pub.Status = RemedialTrackPublicationStatus.Cancelled;
                    pub.CancelledAtUtc = now;
                    pub.CancelReason = cleanReason;
                    batchId = pub.BatchId;

                    // التسجيلات غير المنتهية: تحديث جماعي + أحداث بإدخال جماعي واحد
                    var openIds = await db.RemedialTrackEnrollments.AsNoTracking()
                        .Where(e => e.PublicationId == id
                                    && (e.Status == RemedialTrackEnrollmentStatus.NotStarted
                                        || e.Status == RemedialTrackEnrollmentStatus.InProgress))
                        .Select(e => e.Id)
                        .ToListAsync(token);

                    var message = Truncate($"أُلغي أمر النشر: {cleanReason}", 500);
                    foreach (var enrollmentId in openIds)
                    {
                        db.RemedialTrackEvents.Add(new RemedialTrackEvent
                        {
                            EnrollmentId = enrollmentId,
                            Type = RemedialTrackEventType.EnrollmentCancelled,
                            Message = message,
                            ActorUserId = actor.UserId,
                            ActorName = Truncate(actor.Name, 200),
                            CreatedAtUtc = now
                        });
                    }

                    if (db.Database.IsRelational())
                    {
                        await db.SaveChangesAsync(token); // الأمر + الأحداث
                        await db.RemedialTrackEnrollments
                            .Where(e => e.PublicationId == id
                                        && (e.Status == RemedialTrackEnrollmentStatus.NotStarted
                                            || e.Status == RemedialTrackEnrollmentStatus.InProgress))
                            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, RemedialTrackEnrollmentStatus.Cancelled), token);
                    }
                    else
                    {
                        // InMemory (الاختبارات) لا يدعم ExecuteUpdate
                        var open = await db.RemedialTrackEnrollments
                            .Where(e => e.PublicationId == id
                                        && (e.Status == RemedialTrackEnrollmentStatus.NotStarted
                                            || e.Status == RemedialTrackEnrollmentStatus.InProgress))
                            .ToListAsync(token);
                        foreach (var e in open)
                            e.Status = RemedialTrackEnrollmentStatus.Cancelled;
                        await db.SaveChangesAsync(token);
                    }

                    return RemedialTrackResult.Ok($"✅ أُلغي أمر النشر وأُوقف {openIds.Count} تسجيلًا غير منتهٍ.");
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّل أمر النشر من مستخدم آخر. حدّث الصفحة وأعد المحاولة.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK cancel failed (publication {PublicationId})", id);
                return RemedialTrackResult.Fail("⚠️ تعذّر إلغاء أمر النشر. أعد المحاولة.");
            }

            if (result.Success)
                await LogActivityAsync("RemedialTrack.Cancel", $"[RTK pub:{id}] إلغاء أمر النشر #{id} — السبب: {cleanReason}", actor, batchId);
            return result;
        }

        // ======================================================================
        // RTK v2 / D26: الحذف الناعم والاسترجاع
        // ======================================================================

        public async Task<RemedialTrackResult> DeleteAsync(
            int id, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason) || cleanReason.Length < MinCancelReasonLength || cleanReason.Length > MaxCancelReasonLength)
                return RemedialTrackResult.Fail($"⚠️ سبب الحذف إلزامي ({MinCancelReasonLength}–{MaxCancelReasonLength} حرفًا).");

            var batchId = 0;
            RemedialTrackResult result;
            try
            {
                result = await WriteCoreAsync(async (db, token) =>
                {
                    var pub = await db.RemedialTrackPublications.FirstOrDefaultAsync(p => p.Id == id, token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (pub.IsDeleted)
                        return RemedialTrackResult.Fail("ℹ️ أمر النشر محذوف سابقًا.");

                    // لا يُمس أي تسجيل ولا تقدّم — الإخفاء عبر IsDeleted فقط
                    pub.IsDeleted = true;
                    pub.DeletedAtUtc = Now;
                    pub.DeletedByUserId = Truncate(actor.UserId, 450);
                    pub.DeletedByName = Truncate(actor.Name, 200);
                    pub.DeleteReason = cleanReason;
                    batchId = pub.BatchId;

                    await db.SaveChangesAsync(token);
                    return RemedialTrackResult.Ok("✅ حُذف أمر النشر (حذفًا ناعمًا). اختفى عن الطلاب وتبقى بياناته ويمكن استرجاعه من تبويب «المحذوفة».");
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّل أمر النشر من مستخدم آخر. حدّث الصفحة وأعد المحاولة.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK soft-delete failed (publication {PublicationId})", id);
                return RemedialTrackResult.Fail("⚠️ تعذّر حذف أمر النشر. أعد المحاولة.");
            }

            if (result.Success)
                await LogActivityAsync("RemedialTrackPublicationDeleted", $"[RTK pub:{id}] حذف ناعم لأمر النشر #{id} — السبب: {cleanReason}", actor, batchId);
            return result;
        }

        public async Task<RemedialTrackResult> RestoreAsync(
            int id, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var batchId = 0;
            string? newCode = null;
            RemedialTrackResult result;
            try
            {
                // إعادة المحاولة عند خرق فهرس الرقم المرجعي (2601/2627) بتوليد رقم جديد — نفس آلية الإنشاء
                result = await WriteWithCodeRetryAsync(true, async (db, token) =>
                {
                    newCode = null;
                    var pub = await db.RemedialTrackPublications.FirstOrDefaultAsync(p => p.Id == id, token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (!pub.IsDeleted)
                        return RemedialTrackResult.Fail("ℹ️ أمر النشر غير محذوف.");

                    // الأمر النشط الحضوري يحتفظ برقمه ما لم يستعمله أمر نشط آخر في غيابه
                    if (pub.Status == RemedialTrackPublicationStatus.Active
                        && pub.Mode == RemedialTrackDeliveryMode.InPerson
                        && !string.IsNullOrEmpty(pub.AccessCode))
                    {
                        var code = pub.AccessCode;
                        var taken = await db.RemedialTrackPublications.AsNoTracking()
                            .AnyAsync(p => p.Id != pub.Id && p.AccessCode == code
                                           && p.Status == RemedialTrackPublicationStatus.Active && !p.IsDeleted, token);
                        if (taken)
                        {
                            newCode = await NewUniqueCodeAsync(db, token);
                            if (newCode is null)
                                return RemedialTrackResult.Fail("⚠️ تعذّر توليد رقم مرجعي فريد. أعد المحاولة.");
                            pub.AccessCode = newCode;
                            pub.CodeVersion++;               // يُعاد التحقق من الطلاب بالرقم الجديد
                            pub.CodeGeneratedAtUtc = Now;
                        }
                    }

                    pub.IsDeleted = false;
                    pub.DeletedAtUtc = null;
                    pub.DeletedByUserId = null;
                    pub.DeletedByName = null;
                    pub.DeleteReason = null;
                    batchId = pub.BatchId;

                    await db.SaveChangesAsync(token);
                    return RemedialTrackResult.Ok(newCode is null
                        ? "✅ أُعيد أمر النشر بتقدّم الطلاب كما كان."
                        : "✅ أُعيد أمر النشر بتقدّم الطلاب كما كان، وتم توليد رقم مرجعي جديد لأن رقمه السابق استُعمل في أمر نشط آخر.");
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّل أمر النشر من مستخدم آخر. حدّث الصفحة وأعد المحاولة.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK restore failed (publication {PublicationId})", id);
                return RemedialTrackResult.Fail("⚠️ تعذّر استرجاع أمر النشر. أعد المحاولة.");
            }

            if (result.Success)
                await LogActivityAsync("RemedialTrackPublicationRestored",
                    $"[RTK pub:{id}] استرجاع أمر النشر #{id}{(newCode is null ? string.Empty : " مع رقم مرجعي جديد")}", actor, batchId);
            return result;
        }

        // ======================================================================
        // RTK v2 / D24: وضع مراجعة الفيديوهات (للقراءة فقط — لا يمس تقدّمًا ولا درجات)
        // ======================================================================

        public const int MaxReviewEnrollmentIds = 500;
        public const int MaxReviewDays = 90;

        public async Task<RemedialTrackResult> SetVideoReviewAsync(
            int publicationId, RemedialTrackReviewInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var now = Now;

            List<int>? ids = null;
            if (!input.ApplyToAll)
            {
                ids = (input.EnrollmentIds ?? Array.Empty<int>()).Distinct().ToList();
                if (ids.Count == 0)
                    return RemedialTrackResult.Fail("⚠️ اختر طالبًا واحدًا على الأقل أو طبّق على الجميع.");
                if (ids.Count > MaxReviewEnrollmentIds)
                    return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxReviewEnrollmentIds} طالب للطلب الواحد.");
            }

            // الانتهاء يخص التشغيل فقط؛ الإيقاف يصفّره
            DateTime? untilUtc = null;
            if (input.Enabled && input.UntilUtc is { } requested)
            {
                if (requested <= now)
                    return RemedialTrackResult.Fail("⚠️ تاريخ انتهاء المراجعة يجب أن يكون في المستقبل.");
                if (requested > now.AddDays(MaxReviewDays))
                    return RemedialTrackResult.Fail($"⚠️ أقصى مدة للمراجعة {MaxReviewDays} يومًا.");
                untilUtc = requested;
            }

            var batchId = 0;
            var affected = 0;
            RemedialTrackResult result;
            try
            {
                result = await WriteCoreAsync(async (db, token) =>
                {
                    var pub = await db.RemedialTrackPublications.AsNoTracking()
                        .Where(p => p.Id == publicationId)
                        .Select(p => new { p.BatchId, p.IsDeleted, p.Status })
                        .FirstOrDefaultAsync(token);
                    if (pub is null || !scope.Allows(pub.BatchId))
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير موجود.");
                    if (pub.IsDeleted || pub.Status != RemedialTrackPublicationStatus.Active)
                        return RemedialTrackResult.Fail("⚠️ أمر النشر غير نشط (ملغى أو محذوف).");
                    batchId = pub.BatchId;

                    // تسجيلات الأمر غير الملغاة (الملغاة لا يصلها الطالب أصلًا)
                    var target = db.RemedialTrackEnrollments
                        .Where(e => e.PublicationId == publicationId && e.Status != RemedialTrackEnrollmentStatus.Cancelled);

                    if (ids is not null)
                    {
                        // قائمة ≤ 500 — CompatibilityLevel(120) يولّد IN بثوابت لا OPENJSON
                        target = target.Where(e => ids.Contains(e.Id));
                        var owned = await target.CountAsync(token);
                        if (owned != ids.Count)
                            return RemedialTrackResult.Fail("🚫 بعض التسجيلات المحدّدة لا تتبع هذا الأمر أو ملغاة؛ لم يُنفَّذ شيء.");
                    }

                    var enabled = input.Enabled;
                    var userId = Truncate(actor.UserId, 450);
                    var userName = Truncate(actor.Name, 200);

                    if (db.Database.IsRelational())
                    {
                        // تحديث مجمَّع بلا RowVersion: لا يتعارض مع نبضات الطالب الجارية على نفس التسجيل
                        affected = await target.ExecuteUpdateAsync(s => s
                            .SetProperty(e => e.VideoReviewEnabled, enabled)
                            .SetProperty(e => e.VideoReviewUntilUtc, untilUtc)
                            .SetProperty(e => e.VideoReviewChangedAtUtc, now)
                            .SetProperty(e => e.VideoReviewChangedByUserId, userId)
                            .SetProperty(e => e.VideoReviewChangedByName, userName), token);
                    }
                    else
                    {
                        // InMemory (الاختبارات) لا يدعم ExecuteUpdate
                        var rows = await target.ToListAsync(token);
                        foreach (var e in rows)
                        {
                            e.VideoReviewEnabled = enabled;
                            e.VideoReviewUntilUtc = untilUtc;
                            e.VideoReviewChangedAtUtc = now;
                            e.VideoReviewChangedByUserId = userId;
                            e.VideoReviewChangedByName = userName;
                        }
                        await db.SaveChangesAsync(token);
                        affected = rows.Count;
                    }

                    if (affected == 0)
                        return RemedialTrackResult.Fail("ℹ️ لا توجد تسجيلات نشطة لتطبيق المراجعة عليها.");

                    return RemedialTrackResult.Ok(enabled
                        ? $"✅ فُتح وضع المراجعة لـ {affected} طالبًا (للمشاهدة فقط ولا تتأثر الدرجات)."
                        : $"✅ أُغلق وضع المراجعة لـ {affected} طالبًا.", affected);
                }, ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "RTK video-review update failed (publication {PublicationId})", publicationId);
                return RemedialTrackResult.Fail("⚠️ تعذّر حفظ وضع المراجعة. أعد المحاولة.");
            }

            if (result.Success)
            {
                var untilText = untilUtc is null
                    ? "بلا انتهاء (حتى يغلقه الأدمن)"
                    : "حتى " + _tz.ConvertToSaudi(untilUtc.Value).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                await LogActivityAsync("RemedialTrackVideoReviewChanged",
                    $"[RTK pub:{publicationId}] وضع مراجعة الفيديوهات: {(input.Enabled ? "تشغيل" : "إيقاف")} لـ {affected} تسجيلًا ({(input.ApplyToAll ? "الكل" : "محدّدون")})"
                    + (input.Enabled ? $" — {untilText}" : string.Empty),
                    actor, batchId);
            }
            return result;
        }

        // ======================================================================
        // مساعدات
        // ======================================================================

        private async Task<string?> NewUniqueCodeAsync(ApplicationDbContext db, CancellationToken ct)
        {
            for (var i = 0; i < CodeDrawsPerAttempt; i++)
            {
                var code = _codes.NewAccessCode();
                var taken = await db.RemedialTrackPublications.AsNoTracking()
                    .AnyAsync(p => p.AccessCode == code && p.Status == RemedialTrackPublicationStatus.Active && !p.IsDeleted, ct);
                if (!taken)
                    return code;
            }
            return null;
        }

        private async Task NotifyStudentsAsync(List<int> studentIds, string trackTitle, DateTime publishAtUtc, RemedialTrackDeliveryMode mode)
        {
            if (studentIds.Count == 0)
                return;
            try
            {
                var local = _tz.ConvertToSaudi(publishAtUtc).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                var modeText = mode == RemedialTrackDeliveryMode.InPerson ? "حضوري" : "أونلاين";
                // لا يُرسَل الرقم المرجعي في الإشعار
                var message = $"📘 خطة علاجية جديدة: {trackTitle}. تُتاح بتاريخ {local}. النمط: {modeText}.";
                await _notifications.SendToStudentsAsync(studentIds, message, NotificationCategory.Remedial, "/Students/RemedialTrack");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل إرسال إشعارات الخطة العلاجية ({Count} طالب)", studentIds.Count);
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

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
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

        // خرق الفهرس الفريد للرقم المرجعي (2601/2627) يعيد المحاولة بتوليد رقم جديد (حدّ MaxCodeRetries)
        private async Task<RemedialTrackResult> WriteWithCodeRetryAsync(
            bool retryOnUniqueViolation,
            Func<ApplicationDbContext, CancellationToken, Task<RemedialTrackResult>> body,
            CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await WriteCoreAsync(body, ct);
                }
                catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException
                                                   && retryOnUniqueViolation
                                                   && attempt < MaxCodeRetries
                                                   && IsUniqueViolation(ex))
                {
                    // تعارض على UX_RemedialTrackPublications_ActiveCode — أعد توليد الرقم
                }
            }
        }
    }
}
