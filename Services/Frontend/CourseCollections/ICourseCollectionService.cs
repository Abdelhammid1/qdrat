using QdratNew.ViewModels.Admin.CourseCollections;
using QdratNew.ViewModels.Frontend.CourseCollections;

namespace QdratNew.Services.Frontend.CourseCollections;

public interface ICourseCollectionService
{
    // ── Frontend (Public) ──────────────────────────────────────
    Task<CourseCollectionHomeSectionViewModel> GetHomeSectionAsync();

    Task<CourseCollectionRegisterViewModel?> GetRegisterViewModelAsync(string collectionSlug, string courseSlug);
    Task<(bool Success, int? RegistrationId, string? ErrorMessage)> CreateRegistrationAsync(
        CourseCollectionRegisterViewModel model, string? ip, string? userAgent);
    Task<CourseCollectionThanksViewModel?> GetThanksAsync(int registrationId);

    Task<CourseCollectionMultiRegisterViewModel?> GetMultiRegisterViewModelAsync(string collectionSlug);
    Task<(bool Success, string? ErrorMessage, List<string> RegisteredCourses)> CreateMultiRegistrationAsync(
        string collectionSlug, CourseCollectionMultiRegisterViewModel model, string? ip, string? userAgent);

    // ── Admin - Collections ────────────────────────────────────
    Task<IReadOnlyList<CourseCollectionListItemViewModel>> GetAdminCollectionsAsync();
    Task<CourseCollectionFormViewModel?> GetCollectionFormAsync(int id);
    Task<int> CreateCollectionAsync(CourseCollectionFormViewModel model, string webRootPath);
    Task<bool> UpdateCollectionAsync(CourseCollectionFormViewModel model, string webRootPath);
    Task<bool> ToggleCollectionActiveAsync(int id);
    Task<bool> ToggleCollectionHomeVisibilityAsync(int id);

    // ── Admin - Courses within a Collection ────────────────────
    Task<IReadOnlyList<CourseCollectionCourseListItemViewModel>> GetAdminCoursesAsync(int collectionId);
    Task<CourseCollectionCourseFormViewModel?> GetCourseFormAsync(int id);
    Task<int> CreateCourseAsync(CourseCollectionCourseFormViewModel model, string webRootPath);
    Task<bool> UpdateCourseAsync(CourseCollectionCourseFormViewModel model, string webRootPath);
    Task<bool> ToggleCourseActiveAsync(int id);
    Task<bool> ToggleCourseHomeVisibilityAsync(int id);
    Task<bool> ToggleCourseRegistrationOpenAsync(int id);

    // ── Admin - Sponsors within a Collection ───────────────────
    Task<IReadOnlyList<CourseCollectionSponsorListItemViewModel>> GetAdminSponsorsAsync(int collectionId);
    Task<int> CreateSponsorAsync(CourseCollectionSponsorFormViewModel model, string webRootPath);
    Task<bool> UpdateSponsorAsync(CourseCollectionSponsorFormViewModel model, string webRootPath);
    Task<bool> ToggleSponsorHomeVisibilityAsync(int id);
    Task<bool> DeleteSponsorAsync(int id, string webRootPath);

    // ── Admin - Registrations ──────────────────────────────────
    Task<CourseCollectionDashboardViewModel> GetAdminDashboardAsync(CourseCollectionRegistrationFilterViewModel filter);
    Task<CourseCollectionRegistrationDetailsViewModel?> GetRegistrationDetailsAsync(int id);
    Task<bool> UpdateRegistrationStatusAsync(CourseCollectionRegistrationUpdateStatusViewModel model, string userId, string userName);
}
