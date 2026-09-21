using QdratNew.ViewModels.Admin.ProfessionalCertificates;
using QdratNew.ViewModels.Frontend.ProfessionalCertificates;

namespace QdratNew.Services.Frontend.ProfessionalCertificates;

public interface IProfessionalCertificateService
{
    // Frontend
    Task<ProfessionalCertificateHomeSectionViewModel> GetHomeSectionAsync();
    Task<ProfessionalCertificateRegisterViewModel?> GetRegisterViewModelAsync(string slug);
    Task<(bool Success, int? RegistrationId, string? ErrorMessage)> CreateRegistrationAsync(
        ProfessionalCertificateRegisterViewModel model, string? ip, string? userAgent);
    Task<ProfessionalCertificateThanksViewModel?> GetThanksAsync(int registrationId);

    // Multi-course registration
    Task<ProfessionalCertificateMultiRegisterViewModel> GetMultiRegisterViewModelAsync();
    Task<(bool Success, string? ErrorMessage, List<string> RegisteredCourses)> CreateMultiRegistrationAsync(
        ProfessionalCertificateMultiRegisterViewModel model, string? ip, string? userAgent);

    // Admin - Registrations
    Task<ProfessionalCertificateDashboardViewModel> GetAdminDashboardAsync(
        ProfessionalCertificateRegistrationFilterViewModel filter);
    Task<ProfessionalCertificateRegistrationDetailsViewModel?> GetRegistrationDetailsAsync(int id);
    Task<bool> UpdateRegistrationStatusAsync(
        ProfessionalCertificateRegistrationUpdateStatusViewModel model, string userId, string userName);

    // Admin - Courses
    Task<IReadOnlyList<ProfessionalCertificateCourseListItemViewModel>> GetAdminCoursesAsync();
    Task<ProfessionalCertificateCourseFormViewModel?> GetCourseFormAsync(int id);
    Task<int> CreateCourseAsync(ProfessionalCertificateCourseFormViewModel model, string webRootPath);
    Task<bool> UpdateCourseAsync(ProfessionalCertificateCourseFormViewModel model, string webRootPath);
    Task<bool> ToggleCourseActiveAsync(int id);
    Task<bool> ToggleCourseHomeVisibilityAsync(int id);
    Task<bool> ToggleRegistrationOpenAsync(int id);

    // Admin - Settings
    Task<ProfessionalCertificateSectionSettingViewModel> GetSectionSettingAsync();
    Task<bool> UpdateSectionSettingAsync(ProfessionalCertificateSectionSettingViewModel model, string userId);
}
