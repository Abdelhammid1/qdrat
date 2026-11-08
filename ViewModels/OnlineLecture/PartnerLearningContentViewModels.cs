using QdratNew.Enums;

namespace QdratNew.ViewModels.OnlineLecture
{
    // OLC-S4.3 — منطقة الشريك «المحتوى التعليمي» (قراءة فقط — R19). لا روابط ولا معرّفات فيديو في أي نموذج هنا.

    public class PartnerLearningIndexVm
    {
        public bool FeatureEnabled { get; set; }
        /// <summary>قراءة بعد انتهاء العقد (ضمن AccessUntilDate) — يُعرض تنبيه.</summary>
        public bool IsReadAfterExpiry { get; set; }
        public List<PartnerLearningBatchVm> Batches { get; set; } = new();
    }

    public class PartnerLearningBatchVm
    {
        public int BatchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public int StudentsCount { get; set; }
        public int PublishedCount { get; set; }
        /// <summary>عدد (طالب × محاضرة منشورة) المكتملة — لحساب نسبة الإتمام من بيانات حقيقية.</summary>
        public int CompletedPairs { get; set; }
        public int CompletionPercent => StudentsCount == 0 || PublishedCount == 0
            ? 0
            : (int)Math.Round(CompletedPairs * 100.0 / (StudentsCount * PublishedCount));
    }

    public class PartnerLearningBatchDetailsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public int StudentsCount { get; set; }
        public List<PartnerLearningLectureVm> Lectures { get; set; } = new();
    }

    public class PartnerLearningLectureVm
    {
        public int LectureContentId { get; set; }
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public LectureContentMode Mode { get; set; }
        public int ItemsCount { get; set; }
        public int StartedCount { get; set; }
        public int CompletedCount { get; set; }
    }

    public class PartnerLearningLectureDetailsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public PartnerLearningLectureVm Lecture { get; set; } = new();
        public List<PartnerLearningStudentRowVm> Students { get; set; } = new();
    }

    public class PartnerLearningStudentRowVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool Started { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercent { get; set; }
        /// <summary>IndicatorVideos: عدد الفيديوهات المكتملة.</summary>
        public int CompletedItems { get; set; }
        public DateTime? LastActivityUtc { get; set; }
    }
}
