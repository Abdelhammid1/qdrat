using QdratNew.Enums;
using QdratNew.Services.ProfessionalModels;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S2.1: إرسال واحد من نموذج لعدة مدارس شريكة: Transaction مستقلة لكل مدرسة، وفشل مدرسة لا يلغي غيرها.
    public interface IPartnerModelDispatchService
    {
        // targets ناتج IDispatchAudienceService.ValidateAsync، و request بلا دفعات: تُملأ لكل مدرسة من هدفها.
        Task<PartnerDispatchOutcome> SendHomeworkAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendModelHomeworkRequest request, DispatchActor actor, CancellationToken ct = default);

        Task<PartnerDispatchOutcome> SendExamAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendModelExamRequest request, DispatchActor actor, CancellationToken ct = default);
    }

    public sealed record DispatchActor(string UserId, string? UserName);

    public sealed class PartnerDispatchPartnerResult
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public bool Succeeded { get; set; }
        public string Message { get; set; } = "";
        public int BatchesSent { get; set; }
        public int StudentsSent { get; set; }
        public bool ContractOverridden { get; set; }
    }

    // يُحفظ في TempData لصفحة النتيجة، فالمعاينة لا تُخزَّن
    public sealed class PartnerDispatchOutcome
    {
        public Guid DispatchGroupId { get; set; }
        public PartnerDispatchKind Kind { get; set; }

        // APD-S3.1: مسودة لدى المدرسة لم تصل لأي طالب
        public bool IsDraft { get; set; }

        public List<PartnerDispatchPartnerResult> Results { get; set; } = new();

        // النموذج غير موجود أو نوعه لا يطابق الشاشة: لم يُكتب شيء
        public bool IsNotFound { get; set; }

        // خطأ يخص الطلب كله (المواعيد مثلًا): لم يُكتب شيء
        public string? ValidationError { get; set; }

        // تحذيرات التكرار لكل المدارس في معاينة واحدة: لم يُكتب شيء
        public bool RequiresConfirmation { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public ModelSendPreview? Preview { get; set; }

        public int SucceededCount => Results.Count(r => r.Succeeded);
        public int FailedCount => Results.Count(r => !r.Succeeded);
        public int TotalStudents => Results.Sum(r => r.StudentsSent);
    }
}
