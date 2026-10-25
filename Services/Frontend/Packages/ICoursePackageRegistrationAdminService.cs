using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Services.Frontend.Packages;

/// <summary>صندوق طلبات حجز الباقات في الأدمن (PKG-S5.3).</summary>
public interface ICoursePackageRegistrationAdminService
{
    Task<CoursePackageRegistrationsIndexViewModel> GetIndexAsync(CoursePackageRegistrationFilter filter, CancellationToken ct = default);

    Task<CoursePackageRegistrationDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>false إن لم يوجد الطلب.</summary>
    Task<bool> UpdateAsync(UpdateCoursePackageRegistrationRequest request, string? userId, CancellationToken ct = default);
}
