using System.Text.Json.Serialization;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S2: نفس أسماء الحقول التي يقرؤها JS صفحة CreateAdvanced
    public sealed class PlacementSectionOptionDto
    {
        [JsonPropertyName("curriculumId")] public int CurriculumId { get; init; }
        [JsonPropertyName("curriculumTitle")] public string? CurriculumTitle { get; init; }
        [JsonPropertyName("sectionId")] public int SectionId { get; init; }
        [JsonPropertyName("sectionTitle")] public string? SectionTitle { get; init; }
    }

    // PPL-S2: منطق إدارة اختبارات تحديد المستوى بنطاق. كل ID يُتحقق من ملكيته هنا لا في الـ Controller.
    public interface IPlacementExamManagementService
    {
        Task<PlacementExamsIndexVM> GetIndexAsync(PlacementScope scope, CancellationToken ct = default);

        /// <summary>null = الدفعة خارج النطاق.</summary>
        Task<List<PlacementExamListVm>?> GetBatchExamsAsync(PlacementScope scope, int batchId, CancellationToken ct = default);
        Task<string?> GetBatchNameAsync(PlacementScope scope, int batchId, CancellationToken ct = default);

        Task<PlacementExamCreateAdvancedViewModel> GetCreateAdvancedAsync(PlacementScope scope, CancellationToken ct = default);
        Task ReloadCreateAdvancedLookupsAsync(PlacementScope scope, PlacementExamCreateAdvancedViewModel model, CancellationToken ct = default);
        Task<PlacementOperationResult> CreateAdvancedAsync(PlacementScope scope, PlacementExamCreateAdvancedViewModel model, CancellationToken ct = default);

        Task<PlacementExamFromModelVm> GetCreateFromModelAsync(PlacementScope scope, CancellationToken ct = default);
        Task<PlacementOperationResult> CreateFromProfessionalModelAsync(PlacementScope scope, PlacementExamFromModelVm vm, CancellationToken ct = default);
        Task ReloadCreateFromModelLookupsAsync(PlacementScope scope, PlacementExamFromModelVm vm, CancellationToken ct = default);

        Task<PlacementExamReviewVm?> GetReviewAsync(PlacementScope scope, int examId, CancellationToken ct = default);
        Task<PlacementOperationResult> ToggleDontKnowOptionAsync(PlacementScope scope, int examId, bool enabled, CancellationToken ct = default);

        Task<ReplaceQuestionVm?> GetReplaceQuestionAsync(PlacementScope scope, int examId, Guid questionId, CancellationToken ct = default);
        Task<PlacementOperationResult> ReplaceQuestionAsync(PlacementScope scope, int examId, Guid oldQuestionId, Guid newQuestionId, CancellationToken ct = default);

        /// <summary>Model موجود فقط عند Outcome == Success.</summary>
        Task<(PlacementOperationResult Result, AddQuestionFromBankVm? Model)> GetAddQuestionFromBankAsync(PlacementScope scope, int examId, int sectionId, CancellationToken ct = default);
        Task<PlacementOperationResult> AddQuestionFromBankAsync(PlacementScope scope, int examId, int sectionId, Guid questionId, CancellationToken ct = default);

        Task<PlacementOperationResult> SendToStudentsAsync(PlacementScope scope, int examId, CancellationToken ct = default);
        Task<PlacementOperationResult> SendToNewStudentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default);
        Task<PlacementOperationResult> BackfillMissingAssignmentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default);
        Task<PlacementOperationResult> ResendToStudentAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default);
        Task<PlacementOperationResult> ConfirmSendAsync(PlacementScope scope, int examId, CancellationToken ct = default);

        Task<PlacementOperationResult> HideAsync(PlacementScope scope, int examId, CancellationToken ct = default);
        Task<PlacementOperationResult> RestoreAsync(PlacementScope scope, int examId, CancellationToken ct = default);
        Task<PlacementOperationResult> DeletePermanentlyAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default);

        Task<List<PlacementSectionOptionDto>> GetSectionsByCourseAsync(PlacementScope scope, int courseId, CancellationToken ct = default);
        Task<PlacementExamDetailsViewModel?> GetPlacementDetailsAsync(PlacementScope scope, int examId, CancellationToken ct = default);
    }
}
