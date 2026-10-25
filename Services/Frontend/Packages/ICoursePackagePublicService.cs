using QdratNew.ViewModels.Frontend.Packages;

namespace QdratNew.Services.Frontend.Packages;

public interface ICoursePackagePublicService
{
    /// <summary>باقات الرئيسية (مخزّنة عبر HomePageCache).</summary>
    Task<List<PackageCardViewModel>> GetHomeSectionAsync();

    Task<List<PackageCardViewModel>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>الباقة الفعّالة فقط؛ null إن لم توجد أو كانت غير فعّالة.</summary>
    Task<PackageDetailsViewModel?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>حجز باقة (الاسم + الجوال). الباقة تُحدَّد بالـ slug فقط.</summary>
    Task<SubmitPackageResult> SubmitAsync(string slug, PackageRegisterFormViewModel form, string? ip, CancellationToken ct = default);
}
