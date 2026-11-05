using QdratNew.Enums;

namespace QdratNew.Services.ProfessionalModels
{
    // PPM-S3: الإرسال من نموذج بنطاق Admin/Partner. PPM-S5: الاختبار.
    public interface IProfessionalModelSendService
    {
        Task<IReadOnlyList<ProfessionalModelListItemVm>> GetModelsAsync(ModelSendScope scope, ProfessionalModelType type, CancellationToken ct = default);

        Task<IReadOnlyList<ModelBatchOptionVm>> GetEligibleBatchesAsync(ModelSendScope scope, int modelId, CancellationToken ct = default);

        // null = النموذج غير متاح لهذا النطاق. أخطاء الدفعات والأسئلة في ValidationError.
        Task<ModelSendPreview?> PreviewAsync(ModelSendScope scope, ProfessionalModelType type, SendFromModelRequestBase req, CancellationToken ct = default);

        Task<IReadOnlyList<PreviewQuestionVm>> GetReplacementCandidatesAsync(ModelSendScope scope, int modelId, Guid questionId, CancellationToken ct = default);

        // خطأ مواعيد الإرسال لهذا النطاق، أو null. يفيد المعاينة لأن الإرسال يعيد الفحص بنفسه.
        string? ValidateSchedule(ModelSendScope scope, SendFromModelRequestBase req);

        Task<ModelSendResult> SendHomeworkAsync(ModelSendScope scope, SendModelHomeworkRequest req, CancellationToken ct = default);

        Task<ModelSendResult> SendExamAsync(ModelSendScope scope, SendModelExamRequest req, CancellationToken ct = default);
    }
}
