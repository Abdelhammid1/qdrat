using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QdratNew.Services.ProfessionalModels;

namespace QdratNew.ViewModels.Partner
{
    // PPM-S4: قائمة نماذج الواجب للشريك
    public sealed class PartnerHomeworkModelsVm
    {
        public bool CanCreateHomework { get; init; }
        public IReadOnlyList<ProfessionalModelListItemVm> Models { get; init; } = Array.Empty<ProfessionalModelListItemVm>();
    }

    // PPM-S5.2: قائمة نماذج الاختبار للشريك
    public sealed class PartnerExamModelsVm
    {
        public bool CanCreateExams { get; init; }
        public bool CanUsePlacementExams { get; init; }
        public IReadOnlyList<ProfessionalModelListItemVm> Models { get; init; } = Array.Empty<ProfessionalModelListItemVm>();
    }

    // PPM-S4.1: LectureId غير موجود هنا عمدًا لأن ربط الواجب بمحاضرة للأدمن فقط
    public sealed class SendModelHomeworkVm : SendFromModelVm
    {
        [StringLength(200, ErrorMessage = "عنوان الواجب يجب ألا يتجاوز 200 حرف.")]
        public override string? Title { get; set; }

        [Required(ErrorMessage = "حدد موعد بداية الواجب.")]
        public override DateTime? StartAtLocal { get; set; }

        [Required(ErrorMessage = "حدد موعد نهاية الواجب.")]
        public override DateTime? EndAtLocal { get; set; }

        public override string Noun => "واجب";
        public override string NounDefinite => "الواجب";
        public override string FormAction => "SendHomework";
        public override string CandidatesAction => "ReplacementCandidates";
        public override string ListAction => "Index";
    }

    // PPM-S5.2
    public sealed class SendModelExamVm : SendFromModelVm
    {
        public const int MinDurationMinutes = 5;
        public const int MaxDurationMinutes = 300;

        [StringLength(200, ErrorMessage = "عنوان الاختبار يجب ألا يتجاوز 200 حرف.")]
        public override string? Title { get; set; }

        [Required(ErrorMessage = "حدد موعد بداية الاختبار.")]
        public override DateTime? StartAtLocal { get; set; }

        [Required(ErrorMessage = "حدد موعد نهاية الاختبار.")]
        public override DateTime? EndAtLocal { get; set; }

        [Range(MinDurationMinutes, MaxDurationMinutes, ErrorMessage = "مدة الاختبار يجب أن تكون بين 5 و300 دقيقة.")]
        public int DurationMinutes { get; set; } = 60;

        public override string Noun => "اختبار";
        public override string NounDefinite => "الاختبار";
        public override string FormAction => "SendExam";
        public override string CandidatesAction => "ExamReplacementCandidates";
        public override string ListAction => "Exams";
    }

    // المشترك بين إرسال الواجب والاختبار، وتعتمد عليه الـ Partial الواحدة _SendFromModelForm
    public abstract class SendFromModelVm
    {
        public const string IntentPreview = "preview";
        public const string IntentSend = "send";

        public int ModelId { get; set; }

        public List<int> BatchIds { get; set; } = new();

        public abstract string? Title { get; set; }

        public abstract DateTime? StartAtLocal { get; set; }

        public abstract DateTime? EndAtLocal { get; set; }

        public abstract string Noun { get; }
        public abstract string NounDefinite { get; }
        public abstract string FormAction { get; }
        public abstract string CandidatesAction { get; }
        public abstract string ListAction { get; }

        public List<Guid> ExcludedQuestionIds { get; set; } = new();

        public Dictionary<Guid, Guid> Replacements { get; set; } = new();

        public bool ConfirmWarnings { get; set; }

        // أي قيمة غير send تعني معاينة بلا كتابة
        public string? Intent { get; set; }

        [ValidateNever] public string ModelTitle { get; set; } = "";

        [ValidateNever] public IReadOnlyList<ModelBatchOptionVm> EligibleBatches { get; set; } = Array.Empty<ModelBatchOptionVm>();

        // أسئلة النموذج كما هي قبل الاستبعاد والاستبدال
        [ValidateNever] public IReadOnlyList<PreviewQuestionVm> ModelQuestions { get; set; } = Array.Empty<PreviewQuestionVm>();

        // السؤال الأصلي ← بديله المختار
        [ValidateNever] public IReadOnlyDictionary<Guid, PreviewQuestionVm> ChosenSubstitutes { get; set; } = new Dictionary<Guid, PreviewQuestionVm>();

        // لا يُعرض قسم التأكيد إلا بعد معاينة ناجحة
        [ValidateNever] public ModelSendPreview? Preview { get; set; }

        [ValidateNever] public string? ErrorMessage { get; set; }
    }
}
