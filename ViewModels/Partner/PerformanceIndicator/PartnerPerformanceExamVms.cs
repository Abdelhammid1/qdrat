using QdratNew.ViewModels.PerformanceIndicator;

namespace QdratNew.ViewModels.Partner.PerformanceIndicator
{
    // APD-S4.2: صفحات متابعة مؤشر الأداء في منطقة الشريك. الإنشاء من الأدمن فقط.
    public sealed class PartnerPerformanceExamRowVm
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public string CurriculumTitle { get; init; } = "";
        public string? ReferenceCode { get; init; }
        public bool IsOnline { get; init; }
        public DateTime StartAt { get; init; }
        public DateTime EndAt { get; init; }
        public int QuestionsCount { get; init; }
        public int StudentsCount { get; init; }
        public int CompletedCount { get; init; }
        public List<string> BatchNames { get; set; } = new();
    }

    public sealed class PartnerPerformanceIndexVm
    {
        public bool ShowHidden { get; init; }
        public int HiddenCount { get; init; }
        public bool CanManage { get; init; }
        public List<PartnerPerformanceExamRowVm> Exams { get; init; } = new();
    }

    public sealed class PartnerPerformanceBatchVm
    {
        public int BatchId { get; init; }
        public string Name { get; init; } = "";
    }

    public sealed class PartnerPerformanceStudentVm
    {
        public int StudentId { get; init; }
        public string FullName { get; init; } = "";
    }

    public sealed class PartnerPerformanceStudentsVm
    {
        public PerformanceIndicatorExamStudentsReportViewModel Report { get; init; } = new();
        public string? ReferenceCode { get; init; }
        public bool IsOnline { get; init; }
        public bool IsHidden { get; init; }
        public bool CanManage { get; set; }
        public List<PartnerPerformanceBatchVm> Batches { get; init; } = new();

        // طلاب نشطون في دفعات الاختبار لم يصلهم (التحقوا بعد الإرسال مثلًا)
        public List<PartnerPerformanceStudentVm> NotAssigned { get; init; } = new();

        // من أكمل فقط: من لم يبدأ لا يُحسب راسبًا
        public int PassedCount => Report.Students.Count(s => s.IsCompleted && s.Passed);
        public int NeedPlanCount => Report.Students.Count(s => s.IsCompleted && !s.Passed);

        public double CompletedAverage
        {
            get
            {
                var completed = Report.Students.Where(s => s.IsCompleted).ToList();
                return completed.Count == 0 ? 0 : Math.Round(completed.Average(s => s.ScorePercent ?? 0), 1);
            }
        }
    }

    public sealed class PartnerPerformanceStudentQuestionsVm
    {
        public StudentPerformanceIndicatorQuestionReportViewModel? Report { get; init; }

        // المراجعة تكشف الإجابات الصحيحة، فتُفتح بعد إنهاء الطالب فقط
        public bool StudentCompleted { get; init; }
    }

    public sealed class PartnerPerformanceSectionStatVm
    {
        public string SectionTitle { get; init; } = "";
        public double AverageScore { get; init; }
        public int BelowPassCount { get; init; }
    }

    public sealed class PartnerPerformanceBatchReportVm
    {
        public int ExamId { get; init; }
        public string ExamTitle { get; init; } = "";
        public string CurriculumTitle { get; init; } = "";
        public int BatchId { get; init; }
        public string BatchName { get; init; } = "";
        public double PassPercent { get; init; }
        public int TotalStudents { get; init; }
        public int CompletedStudents { get; init; }
        public int InProgressStudents { get; init; }
        public int NotStartedStudents { get; init; }
        public int PassedStudents { get; init; }
        public int NeedPlanStudents { get; init; }
        public double AverageScore { get; init; }
        public List<PartnerPerformanceSectionStatVm> Sections { get; init; } = new();
        public List<PerformanceIndicatorExamStudentRowViewModel> Students { get; init; } = new();
        public List<PartnerPerformanceBatchVm> Batches { get; init; } = new();
    }

    public enum PartnerPerformanceResendOutcome
    {
        NotFound = 0,
        Added = 1,
        Reopened = 2,
        AlreadyAvailable = 3,
        AlreadyCompleted = 4,
        NotEligible = 5
    }
}
