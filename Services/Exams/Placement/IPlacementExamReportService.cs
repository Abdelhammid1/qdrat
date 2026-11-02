using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Partner;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S3: Success فيها Model؛ NotFound وNoData فيهما Message (BatchId عند NoData إن وُجد للتحويل).
    public enum PlacementReportOutcome { Success, NotFound, NoData }

    public sealed class PlacementReportResult<T> where T : class
    {
        public PlacementReportOutcome Outcome { get; init; }
        public T? Model { get; init; }
        public string Message { get; init; } = string.Empty;
        public int? BatchId { get; init; }

        public static PlacementReportResult<T> Ok(T model)
            => new() { Outcome = PlacementReportOutcome.Success, Model = model };

        public static PlacementReportResult<T> NotFound(string message = "")
            => new() { Outcome = PlacementReportOutcome.NotFound, Message = message };

        public static PlacementReportResult<T> NoData(string message, int? batchId = null)
            => new() { Outcome = PlacementReportOutcome.NoData, Message = message, BatchId = batchId };
    }

    // PPL-S3: موديل BatchStudents + القيم التي تعرضها الصفحة خارج الموديل
    public sealed class PlacementBatchStudentsData
    {
        public string? BatchName { get; init; }
        public string? ExamTitle { get; init; }
        public List<PlacementExamStudentVm> Students { get; init; } = new();
    }

    // PPL-S3: تقارير اختبارات تحديد المستوى بنطاق. كل ID يُتحقق من ملكيته هنا لا في الـ Controller.
    // ما هو خارج النطاق يعامَل كغير موجود، حتى لا يُكشف وجوده.
    public interface IPlacementExamReportService
    {
        /// <summary>null = الاختبار أو الدفعة خارج النطاق.</summary>
        Task<PlacementBatchStudentsData?> GetBatchStudentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default);

        /// <summary>null = الاختبار أو الطالب خارج النطاق.</summary>
        Task<StudentExamDetailsVm?> GetStudentExamDetailsAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default);

        Task<PlacementReportResult<AdminExamSectionAnalysisViewModel>> GetSectionAnalysisAsync(PlacementScope scope, int assignmentId, int studentId, CancellationToken ct = default);
        Task<PlacementReportResult<ExamReviewViewModel>> GetReviewLessonQuestionsAsync(PlacementScope scope, int assignmentId, int studentId, int lessonId, CancellationToken ct = default);

        /// <summary>null = الاختبار أو الدرس خارج النطاق.</summary>
        Task<BatchLessonQuestionsReportViewModel?> GetReviewLessonQuestionsBatchAsync(PlacementScope scope, int examId, int lessonId, CancellationToken ct = default);

        Task<PlacementReportResult<AdminExamSectionAnalysisViewModel>> GetBatchPerformanceReportAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default);

        Task<PlacementReportResult<PlacementReportViewModel>> GetStudentPlacementReportAsync(PlacementScope scope, int studentId, int assignmentId, int examId, int batchId, CancellationToken ct = default);
        Task<PlacementReportResult<AdminPlacementReportViewModel>> GetStudentPlacementReportPrintAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default);

        /// <summary>null = الاختبار أو الطالب خارج النطاق.</summary>
        Task<(byte[] Pdf, string FileName)?> BuildStudentReportPdfAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default);

        /// <summary>PPL-S6: اسم الشريك وشعاره لترويسة التقرير. null لنطاق الأدمن.</summary>
        Task<PartnerPlacementBrandingVm?> GetReportBrandingAsync(PlacementScope scope, CancellationToken ct = default);

        /// <summary>PPL-S6: اختبارات تحديد المستوى المرسلة لهذا الطالب داخل النطاق. طالب خارج النطاق = قائمة فارغة.</summary>
        Task<List<PartnerPlacementStudentExamVm>> GetStudentPlacementExamsAsync(PlacementScope scope, int studentId, CancellationToken ct = default);

        /// <summary>يفحص النطاق قبل تشغيل Chrome. reportUrl يبنيه الـ Controller لأنه يعرف Route منطقته. null = خارج النطاق.</summary>
        Task<(byte[] Pdf, string FileName)?> BuildStudentReportChromePdfAsync(PlacementScope scope, int examId, int studentId, string reportUrl, CancellationToken ct = default);
    }
}
