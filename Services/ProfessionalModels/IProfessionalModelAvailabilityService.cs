using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.ProfessionalModel;

namespace QdratNew.Services.ProfessionalModels
{
    // PPM-S1.2: المصدر الوحيد لقاعدة إتاحة النماذج الاحترافية للشركاء
    public interface IProfessionalModelAvailabilityService
    {
        // النماذج المتاحة لشريك (تتضمن فحص العقد CanUseProfessionalModels والتاريخ)
        IQueryable<ProfessionalModel> AvailableForPartner(int partnerId, ProfessionalModelType? type);

        Task<bool> IsAvailableForPartnerAsync(int modelId, int partnerId, ProfessionalModelType? type, CancellationToken ct = default);

        // الشركاء النشطون + أي شريك منتهٍ مختار سابقًا
        Task<List<PartnerOptionVm>> GetPartnerOptionsAsync(IEnumerable<int> selectedPartnerIds, CancellationToken ct = default);

        Task<List<int>> GetAssignedPartnerIdsAsync(int modelId, CancellationToken ct = default);

        // يُسقط أي Id غير موجود فعليًا
        Task<List<int>> FilterExistingPartnerIdsAsync(IEnumerable<int> partnerIds, CancellationToken ct = default);

        // يعدّل النموذج المتتبَّع ويزامن روابط الشركاء. المعاملة (Transaction) مسؤولية المستدعي.
        Task ApplyPartnerAudienceAsync(ProfessionalModel model, bool isForPartners, PartnerAudience audience, IReadOnlyCollection<int> partnerIds, CancellationToken ct = default);

        // يزامن روابط فترات الاشتراك (الخيار المتقدم في صفحة Visibility فقط)
        Task SyncSubscriptionPeriodsAsync(int modelId, IEnumerable<int> periodIds, CancellationToken ct = default);

        // PPM-S2: يصنّف النماذج النشطة بلا نوع فقط، ويرجع عدد ما صُنِّف
        Task<int> ClassifyUntypedModelsAsync(IEnumerable<int> modelIds, ProfessionalModelType type, CancellationToken ct = default);
    }
}
