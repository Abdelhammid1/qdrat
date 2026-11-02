using QdratNew.ViewModels.Exam;

namespace QdratNew.ViewModels.Partner
{
    // PPL-S4.2
    public class PartnerPlacementIndexVm
    {
        public List<PlacementBatchExamVm> Batches { get; set; } = new();
        public List<PlacementExamListVm> SingleStudentExams { get; set; } = new();
        public DateTime? ContractEndDate { get; set; }
        public bool CanUseProfessionalModels { get; set; }
        public bool HasBankCourses { get; set; }
    }

    // PPL-S4.2
    public class PartnerPlacementBatchExamsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public List<PlacementExamListVm> Exams { get; set; } = new();
    }

    // PPL-S6: صفحات التقارير. Report هو موديل التقرير نفسه من الخدمة المشتركة،
    // والباقي للروابط وإخفاء الإدارة بعد انتهاء العقد.
    public class PartnerPlacementReportPageVm<TReport> where TReport : class
    {
        public TReport Report { get; set; } = default!;
        public int ExamId { get; set; }
        public int BatchId { get; set; }
        public int StudentId { get; set; }
        public int AssignmentId { get; set; }
        public bool IsReadOnly { get; set; }
        public PartnerPlacementBrandingVm? Branding { get; set; }
    }

    // PPL-S6
    public class PartnerPlacementBrandingVm
    {
        public string PartnerName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }

    // PPL-S6.3: قسم تحديد المستوى في Partner/Reports/Student
    public class PartnerPlacementStudentExamVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int AssignmentId { get; set; }
        public int BatchId { get; set; }
        public DateTime AssignedAt { get; set; }
        public bool IsSubmitted { get; set; }
        public int? Score { get; set; }
    }
}
