using QdratNew.Entities;
using QdratNew.Entities.Frontend;

namespace QdratNew.ViewModels.Public.HomePage;

/// <summary>
/// لقطة مُخزَّنة مشتركة بين كل الزوار — للقراءة فقط داخل الـ Views. لا تعدّل أي عنصر فيها.
/// الكيانات مُحمَّلة بـ AsNoTracking.
/// </summary>
public sealed class HomePageContentViewModel
{
    public IReadOnlyList<HeroSlide> HeroSlides { get; init; } = Array.Empty<HeroSlide>();
    public IReadOnlyList<FrontendCourse> ProgramsCourses { get; init; } = Array.Empty<FrontendCourse>();
    public StaticPage? AboutPage { get; init; }
    public StaticPage? VisionPage { get; init; }
    public StaticPage? MissionPage { get; init; }
    public StaticPage? SaudiVisionPage { get; init; }
    public IReadOnlyList<SuccessPartner> SuccessPartners { get; init; } = Array.Empty<SuccessPartner>();
    public IReadOnlyList<PlatformGoal> PlatformGoals { get; init; } = Array.Empty<PlatformGoal>();
    public bool ShowPetrojetPartnerSection { get; init; } = true;
}

public sealed class FrontendNavViewModel
{
    public IReadOnlyList<FrontendCourse> MainLinks { get; init; } = Array.Empty<FrontendCourse>();
    public IReadOnlyList<FrontendCourse> NavbarCourses { get; init; } = Array.Empty<FrontendCourse>();
    public ILookup<int, SubCourse> SubCoursesByCourse { get; init; } =
        Array.Empty<SubCourse>().ToLookup(s => s.FrontendCourseId);
}
