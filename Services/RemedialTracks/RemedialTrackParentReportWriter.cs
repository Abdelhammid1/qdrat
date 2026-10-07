using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S11.2 (D23/D28): إنشاء لقطة تقرير ولي الأمر وإشعاره — داخل معاملة آلة الحالة (نفس DbContext).
    /// لا يعتمد على خدمات أخرى (الإشعار صفّ Notification في نفس المعاملة فيتّسق مع التقرير)، ولا يُفشل التسليم عند تعذّر بناء اللقطة.
    /// </summary>
    public static class RemedialTrackParentReportWriter
    {
        public const int MaxSnapshotBytes = 64 * 1024;

        private static readonly JsonSerializerOptions Json = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // العربية بلا \uXXXX (توفير الحجم)؛ العرض بترميز Razor
            WriteIndented = false
        };

        // ---------- اللقطة ----------

        public static RemedialTrackParentReportSnapshot ToSnapshot(RemedialTrackEnrollmentReportVm vm, int? focusAxisOrder)
        {
            var focus = focusAxisOrder.HasValue ? vm.Axes.FirstOrDefault(a => a.Order == focusAxisOrder.Value) : null;
            return new RemedialTrackParentReportSnapshot
            {
                StudentName = vm.StudentName,
                BatchName = vm.BatchName,
                TrackTitle = vm.TrackTitle,
                CurriculumTitle = vm.CurriculumTitle,
                IssuedAtUtc = vm.IssuedAtUtc,
                PassPercent = vm.PassPercent,
                IsFinished = vm.IsFinished,
                FocusAxisOrder = focus?.Order,
                FocusAxisTitle = focus?.Title,
                TotalAxes = vm.TotalAxes,
                PassedAxes = vm.PassedAxes,
                FollowUpAxes = vm.FollowUpAxes,
                AverageScorePercent = vm.AverageScorePercent,
                Axes = vm.Axes.Select(a => new RemedialTrackParentReportSnapshotAxis
                {
                    Order = a.Order,
                    Title = a.Title,
                    Outcome = a.Outcome,
                    PathKind = a.PathKind,
                    VideoTotal = a.VideoTotal,
                    VideosDoneRound1 = a.VideosDoneRound1,
                    VideosDoneRound2 = a.VideosDoneRound2,
                    HasRound2 = a.HasRound2,
                    WatchedMinutes = a.WatchedMinutes,
                    VideoMinutes = a.VideoMinutes,
                    Exam101Percent = a.Exam101Percent,
                    Exam102Percent = a.Exam102Percent,
                    PassedAtUtc = a.PassedAtUtc,
                    AdminOpenedAtUtc = a.AdminOpenedAtUtc,
                    Recommendation = a.Recommendation,
                    Attempts = a.Attempts.Select(t => new RemedialTrackParentReportSnapshotAttempt
                    {
                        ExamNumber = t.ExamNumber,
                        SubmittedAtUtc = t.SubmittedAtUtc,
                        CorrectCount = t.CorrectCount,
                        TotalQuestions = t.TotalQuestions,
                        ScorePercent = t.ScorePercent,
                        IsPassed = t.IsPassed,
                        DurationMinutes = t.DurationMinutes
                    }).ToList()
                }).ToList()
            };
        }

        /// <summary>يسلسل اللقطة بحد أقصى 64KB: عند التجاوز تُحذف تفاصيل المحاولات ثم يُعاد الفحص؛ وإن بقيت أكبر ← null.</summary>
        public static string? Serialize(RemedialTrackParentReportSnapshot snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot, Json);
            if (Encoding.UTF8.GetByteCount(json) <= MaxSnapshotBytes) return json;

            foreach (var a in snapshot.Axes) a.Attempts.Clear();
            json = JsonSerializer.Serialize(snapshot, Json);
            return Encoding.UTF8.GetByteCount(json) <= MaxSnapshotBytes ? json : null;
        }

        public static RemedialTrackParentReportSnapshot? TryDeserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
            try
            {
                var s = JsonSerializer.Deserialize<RemedialTrackParentReportSnapshot>(json, Json);
                return s is { Version: > 0 } && !string.IsNullOrEmpty(s.StudentName) ? s : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ---------- الإنشاء داخل المعاملة ----------

        /// <summary>
        /// يبني ويسلسل لقطة التقرير (RTK-S12.1: يُعاد استخدامه عند الإرسال اليدوي لصف Pending لم تُبنَ لقطته).
        /// أي فشل (بناء أو حجم &gt; 64KB) ← Json=null بلا استثناء، والقرار لمن يستدعي.
        /// </summary>
        public static async Task<(string? Json, string? AxisTitle)> TryBuildSnapshotJsonAsync(
            ApplicationDbContext db, int enrollmentId, int? focusAxisOrder, DateTime now, ILogger logger, CancellationToken ct)
        {
            try
            {
                var vm = await RemedialTrackReportBuilder.BuildAsync(db, enrollmentId, null, null, includeNote: false, now, ct);
                if (vm is null) return (null, null);
                var snap = ToSnapshot(vm, focusAxisOrder);
                return (Serialize(snap), snap.FocusAxisTitle);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "RTK: تعذّر بناء لقطة تقرير ولي الأمر (تسجيل {EnrollmentId})", enrollmentId);
                return (null, null);
            }
        }

        /// <summary>
        /// ينشئ تقريرًا واحدًا لـ (تسجيل، محور، نوع) إن لم يوجد (Idempotent) ويحفظه؛ ثم يرسل إشعار ولي الأمر عند التفعيل التلقائي.
        /// المتطلب: حالة المحور/التسجيل محفوظة قبل الاستدعاء (اللقطة تُقرأ من القاعدة).
        /// </summary>
        public static async Task AddAsync(
            ApplicationDbContext db, int enrollmentId, int? axisProgressId, int? focusAxisOrder,
            RemedialTrackParentReportKind kind, DateTime now, ILogger logger, CancellationToken ct)
        {
            var exists = await db.RemedialTrackParentReports.AsNoTracking()
                .AnyAsync(r => r.EnrollmentId == enrollmentId && r.AxisProgressId == axisProgressId && r.Kind == kind, ct);
            if (exists) return;

            var ctx = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId)
                .Select(e => new
                {
                    e.StudentId,
                    StudentName = e.Student!.FullName,
                    ParentId = e.Student.ParentId,
                    ParentUserId = e.Student.Parent != null ? e.Student.Parent.UserId : null,
                    e.Publication!.AutoSendParentReports
                })
                .FirstOrDefaultAsync(ct);
            if (ctx is null) return;

            // بناء اللقطة: فشله لا يُفشل تسليم الاختبار — يبقى الصف Pending لإعادة المحاولة من طابور الأدمن
            var (json, axisTitle) = await TryBuildSnapshotJsonAsync(db, enrollmentId, focusAxisOrder, now, logger, ct);

            var hasParent = ctx.ParentId.HasValue && !string.IsNullOrEmpty(ctx.ParentUserId);
            var status = !hasParent
                ? RemedialTrackParentReportStatus.NoParent
                : (json is null || !ctx.AutoSendParentReports ? RemedialTrackParentReportStatus.Pending : RemedialTrackParentReportStatus.Sent);

            var report = new RemedialTrackParentReport
            {
                EnrollmentId = enrollmentId,
                AxisProgressId = axisProgressId,
                Kind = kind,
                StudentId = ctx.StudentId,
                ParentId = ctx.ParentId,
                SnapshotJson = json ?? "{}",
                Status = status,
                CreatedAtUtc = now,
                SentAtUtc = status == RemedialTrackParentReportStatus.Sent ? now : null
            };
            db.RemedialTrackParentReports.Add(report);
            await db.SaveChangesAsync(ct);   // يولّد Id لرابط الإشعار

            if (status == RemedialTrackParentReportStatus.Sent)
            {
                AddNotification(db, ctx.ParentUserId!, ctx.ParentId!.Value, ctx.StudentId,
                    RemedialTrackParentCopy.NotificationMessage(kind, ctx.StudentName, axisTitle),
                    ReportUrl(report.Id), now);
                await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// عند فتح الإدارة المحور التالي: إشعار نصي لولي الأمر (قرار V1: بلا تقرير تابع) إن كان له ولي أمر والإرسال التلقائي مفعّل.
        /// يربط الإشعار بتقرير «عدم الاجتياز» المُرسل لهذا المحور إن وُجد.
        /// </summary>
        public static async Task AddAdminOpenedNoticeAsync(
            ApplicationDbContext db, int enrollmentId, int axisProgressId, string? axisTitle, DateTime now, CancellationToken ct)
        {
            var ctx = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId)
                .Select(e => new
                {
                    e.StudentId,
                    StudentName = e.Student!.FullName,
                    ParentId = e.Student.ParentId,
                    ParentUserId = e.Student.Parent != null ? e.Student.Parent.UserId : null,
                    e.Publication!.AutoSendParentReports
                })
                .FirstOrDefaultAsync(ct);
            if (ctx is null || !ctx.ParentId.HasValue || string.IsNullOrEmpty(ctx.ParentUserId) || !ctx.AutoSendParentReports) return;

            var reportId = await db.RemedialTrackParentReports.AsNoTracking()
                .Where(r => r.EnrollmentId == enrollmentId && r.AxisProgressId == axisProgressId
                            && r.Kind == RemedialTrackParentReportKind.AxisNotPassed
                            && r.Status == RemedialTrackParentReportStatus.Sent)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(ct);

            AddNotification(db, ctx.ParentUserId, ctx.ParentId.Value, ctx.StudentId,
                RemedialTrackParentCopy.AdminOpenedNextNotice(ctx.StudentName, axisTitle),
                reportId.HasValue ? ReportUrl(reportId.Value) : "/Parents/RemedialTrackReports", now);
            await db.SaveChangesAsync(ct);
        }

        public static string ReportUrl(int reportId) => $"/Parents/RemedialTrackReports/Details?id={reportId}";

        /// <summary>R6: SentAt صريح بالتوقيت العالمي (الافتراضي DateTime.Now في الكيان).</summary>
        public static void AddNotification(
            ApplicationDbContext db, string parentUserId, int parentId, int studentId, string message, string targetUrl, DateTime nowUtc)
            => db.Notifications.Add(new Notification
            {
                UserId = parentUserId,
                ParentID = parentId,
                StudentID = studentId,
                Message = message.Length <= 500 ? message : message[..500],
                Category = NotificationCategory.Remedial,
                TargetUrl = targetUrl,
                SentAt = nowUtc,
                IsRead = false
            });
    }
}
