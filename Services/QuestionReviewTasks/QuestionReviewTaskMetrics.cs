using QdratNew.Enums;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// تعريف النسب والتوقيت لمهام المراجعة — مكان واحد تستخدمه كل الشاشات (QRT-S3).
    /// </summary>
    public static class QuestionReviewTaskMetrics
    {
        private static readonly Lazy<TimeZoneInfo> DisplayZone = new(() =>
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"); }
        });

        public static int Effective(int total, int removed) => Math.Max(total - removed, 0);

        /// <summary>نسبة الاعتماد: ما اعتُمد من الأسئلة الفعلية في المهمة.</summary>
        public static int ApprovalPercent(int approved, int effective)
            => effective <= 0 ? 0 : (int)Math.Round(approved * 100.0 / effective);

        /// <summary>نسبة الإنجاز: ما تم التصرف فيه (اعتماد أو إرجاع).</summary>
        public static int HandledPercent(int pending, int effective)
            => effective <= 0 ? 0 : (int)Math.Round((effective - pending) * 100.0 / effective);

        public static bool IsActive(QuestionReviewTaskStatus status)
            => status == QuestionReviewTaskStatus.Assigned || status == QuestionReviewTaskStatus.InProgress;

        public static bool IsOverdue(QuestionReviewTaskStatus status, DateTime? dueUtc, DateTime nowUtc)
            => IsActive(status) && dueUtc.HasValue && dueUtc.Value < nowUtc;

        /// <summary>D9: تحويل إدخال الأدمن (توقيت Arab Standard Time) إلى UTC للتخزين.</summary>
        public static DateTime LocalToUtc(DateTime local)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), DisplayZone.Value);

        /// <summary>D9: التخزين UTC والعرض بتوقيت Arab Standard Time.</summary>
        public static string? FormatLocal(DateTime? utc, string format = "yyyy/MM/dd HH:mm")
            => utc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), DisplayZone.Value).ToString(format)
                : null;
    }
}
