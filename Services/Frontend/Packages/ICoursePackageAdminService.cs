using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Services.Frontend.Packages;

public enum DeletePackageResult
{
    Deleted = 1,
    NotFound = 2,
    HasRegistrations = 3
}

public interface ICoursePackageAdminService
{
    Task<List<CoursePackageListItemViewModel>> GetListAsync(CancellationToken ct = default);
    Task<CoursePackageFormViewModel?> GetFormAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, int Id, Dictionary<string, string> Errors)> CreateAsync(CoursePackageFormViewModel vm, string webRootPath, string? userId, CancellationToken ct = default);
    Task<(bool Found, Dictionary<string, string> Errors)> UpdateAsync(CoursePackageFormViewModel vm, string webRootPath, string? userId, CancellationToken ct = default);
    Task<bool> ToggleActiveAsync(int id, CancellationToken ct = default);
    Task<bool> ToggleHomeVisibilityAsync(int id, CancellationToken ct = default);
    Task<DeletePackageResult> DeleteAsync(int id, string webRootPath, CancellationToken ct = default);
}
