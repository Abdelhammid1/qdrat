using QdratNew.Enums;

namespace QdratNew.Services.ProfessionalModels
{
    // PPM-S3.1: عقود خدمة الإرسال من نموذج
    public abstract class SendFromModelRequestBase
    {
        public int ModelId { get; set; }
        public List<int> BatchIds { get; set; } = new();
        public string? Title { get; set; }

        // توقيت السعودية كما أُدخل في النموذج
        public DateTime? StartAtLocal { get; set; }
        public DateTime? EndAtLocal { get; set; }

        public List<Guid> ExcludedQuestionIds { get; set; } = new();

        // السؤال القديم ← بديله من نفس الدرس
        public Dictionary<Guid, Guid> Replacements { get; set; } = new();

        // تأكيد تحذيرات التكرار
        public bool ConfirmWarnings { get; set; }
    }

    public sealed class SendModelHomeworkRequest : SendFromModelRequestBase
    {
        // للأدمن فقط ولدفعة واحدة
        public int? LectureId { get; set; }
    }

    public sealed class SendModelExamRequest : SendFromModelRequestBase
    {
        public int DurationMinutes { get; set; }

        // خيارات شاشة الأدمن فقط (PPM-S6.2)
        public bool IsInLab { get; set; }
        public bool? RandomizeQuestions { get; set; }

        // null = كل طلاب الدفعات
        public List<int>? StudentIds { get; set; }
    }

    public sealed class ProfessionalModelListItemVm
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public int? CurriculumId { get; init; }
        public string? CurriculumTitle { get; init; }
        public ProfessionalModelType ModelType { get; init; }
        public int QuestionCount { get; init; }
    }

    public sealed class ModelBatchOptionVm
    {
        public int BatchId { get; init; }
        public string Name { get; init; } = "";
        public int ActiveStudents { get; init; }
        public bool AlreadySentFromModel { get; init; }
    }

    public sealed class PreviewQuestionVm
    {
        public Guid QuestionId { get; init; }
        public int OrderNumber { get; init; }
        public int LessonId { get; init; }
        public string LessonTitle { get; init; } = "";
        public string? Title { get; init; }
        public string? ImageUrl { get; init; }
        public string? ReferenceNumber { get; init; }
        public bool IsQuantitative { get; init; }

        // السؤال الذي حل هذا السؤال محله، إن وُجد
        public Guid? ReplacesQuestionId { get; init; }
    }

    public sealed class BatchWarningVm
    {
        public int BatchId { get; init; }
        public string Name { get; init; } = "";
        public int StudentCount { get; init; }
        public int RepeatedQuestionCount { get; init; }
        public bool AlreadySentFromModel { get; init; }
    }

    public sealed class ModelSendPreview
    {
        public int ModelId { get; init; }
        public string ModelTitle { get; init; } = "";
        public ProfessionalModelType ModelType { get; init; }

        // بعد الاستبعاد والاستبدال
        public List<PreviewQuestionVm> Questions { get; init; } = new();
        public List<BatchWarningVm> Batches { get; init; } = new();

        // خطأ في الدفعات أو الأسئلة المختارة، ورسالته آمنة للعرض
        public string? ValidationError { get; init; }

        public bool HasWarnings => Batches.Any(b => b.RepeatedQuestionCount > 0 || b.AlreadySentFromModel);
    }

    public sealed class ModelSendResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = "";

        // النموذج غير متاح لهذا النطاق: الـ Controller يرجع NotFound
        public bool IsNotFound { get; init; }

        public bool RequiresConfirmation { get; init; }
        public ModelSendPreview? Preview { get; init; }
        public int BatchesSent { get; init; }
        public int StudentsSent { get; init; }

        public static ModelSendResult Fail(string message) => new() { Succeeded = false, Message = message };
    }
}
