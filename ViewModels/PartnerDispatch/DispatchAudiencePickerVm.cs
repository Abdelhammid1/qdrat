using QdratNew.Services.PartnerDispatch;

namespace QdratNew.ViewModels.PartnerDispatch
{
    // APD-S1.4: نموذج Partial اختيار جمهور الإرسال (_DispatchAudiencePicker) في شاشات الأدمن
    public sealed class DispatchAudiencePickerVm
    {
        public DispatchSource Source { get; init; } = null!;

        public IReadOnlyList<DispatchBatchVm> PlatformBatches { get; init; } = Array.Empty<DispatchBatchVm>();

        public AudienceSelection Selection { get; init; } = new();

        // صلاحية PartnerDispatch.Send: بدونها لا يظهر خيار «المدارس الشريكة»
        public bool CanSendToPartners { get; init; }

        // قائمة الشركاء مُعدّة من الخادم (عند إعادة عرض الفورم بعد خطأ). null = تُحمَّل عند اختيار «المدارس الشريكة».
        public DispatchAudiencePartnersVm? Partners { get; init; }

        // محدد CSS لحقل النموذج في الصفحة: تغيّره يعيد تحميل المدارس المؤهلة (APD-S2.2)
        public string? ModelInputSelector { get; set; }

        // APD-S3.1: محدد CSS لحقل الكورس. تغيّره يعيد تحميل المدارس، ودفعاتها تُفلتر به.
        public string? CourseInputSelector { get; set; }

        // APD-S3.1: لا تُحمَّل المدارس قبل اختيار الكورس (الاختبار الواحد يتبع كورسًا واحدًا)
        public bool RequiresCourse { get; set; }

        // APD-S3.1: دفعة منصة واحدة (Radio) بدل عدة دفعات
        public bool SinglePlatformBatch { get; set; }

        // APD-S4.1: محدد CSS لحقل المنهج. تغيّره يعيد تحميل المدارس، ولا تُحمَّل قبل اختياره.
        public string? CurriculumInputSelector { get; set; }
        public bool RequiresCurriculum { get; set; }

        // APD-S4.1: قالب مؤشر الأداء يُرسل للمدارس فقط، فلا يظهر خيار المنصة
        public bool PartnersOnly { get; set; }

        // APD-S5.1: دفعات منصة مُسنَد لها المصدر من قبل (معمل القياس): تُعلَّم بوسم، واختيارها يُسند لطلابها الجدد فقط
        public IReadOnlyCollection<int> AlreadyAssignedPlatformBatchIds { get; set; } = Array.Empty<int>();
    }

    // APD-S1.4: قائمة المدارس ودفعاتها (_DispatchAudiencePartners) — تُعرض داخل الـ Picker وتُرجعها AudienceOptions
    public sealed class DispatchAudiencePartnersVm
    {
        public IReadOnlyList<PartnerEligibilityVm> Partners { get; init; } = Array.Empty<PartnerEligibilityVm>();

        // صلاحية PartnerDispatch.Override
        public bool CanOverride { get; init; }

        public AudienceSelection Selection { get; init; } = new();

        public bool HasOverridablePartners => Partners.Any(p => !p.Eligible && p.Overridable);

        public bool IsPartnerSelected(int partnerId)
            => Selection.PartnerBatchIds.TryGetValue(partnerId, out var ids) && ids is { Count: > 0 };

        // دفعات الشريك المختار سابقًا تُعاد كما اختيرت، وغيره: كل دفعاته التي فيها طلاب مُحددة افتراضيًا
        public bool IsBatchChecked(int partnerId, DispatchBatchVm batch)
        {
            if (batch.ActiveStudents == 0)
                return false;

            return !IsPartnerSelected(partnerId) || Selection.PartnerBatchIds[partnerId].Contains(batch.BatchId);
        }
    }
}
