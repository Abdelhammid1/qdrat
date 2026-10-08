using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: محاولة اختبار 101/102 (محرك خفيف مستقل — D2).</summary>
    public class RemedialTrackExamAttempt
    {
        public int Id { get; set; }
        public int AxisProgressId { get; set; }
        public RemedialTrackAxisProgress? AxisProgress { get; set; }
        public RemedialTrackExamNumber ExamNumber { get; set; }
        /// <summary>RTK-S13: يُملأ لمحاولة «الملحق» فقط (ExamNumber = Addendum) — AxisProgressId يبقى محور الملحق للملكية فقط.</summary>
        public int? AddendumId { get; set; }
        public RemedialTrackAddendum? Addendum { get; set; }
        public int ModelId { get; set; }                     // ProfessionalModel وقت البدء
        public RemedialTrackAttemptStatus Status { get; set; } = RemedialTrackAttemptStatus.InProgress;

        public DateTime StartedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }

        public ICollection<RemedialTrackExamAttemptQuestion> Questions { get; set; } = new List<RemedialTrackExamAttemptQuestion>();
    }
}
