using QdratNew.ViewModels.Public.HomePage;

namespace QdratNew.Services.Frontend.HomePage;

public interface IHomePageContentService
{
    Task<HomePageContentViewModel> GetHomeContentAsync();
    Task<FrontendNavViewModel> GetNavigationAsync();
}
