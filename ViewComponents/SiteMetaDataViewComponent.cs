using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Interfaces;

namespace QdratNew.ViewComponents
{
    public class SiteMetaDataViewComponent : ViewComponent
    {
        private readonly ISystemSettingService _settings;

        public SiteMetaDataViewComponent(ISystemSettingService settings)
        {
            _settings = settings;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new Dictionary<string, string>
            {
                ["SiteName"] = await _settings.GetAsync("SiteName") ?? "منصة قدرات",
                ["SiteMetaDescription"] = await _settings.GetAsync("SiteMetaDescription") ?? "",
                ["SiteMetaKeywords"] = await _settings.GetAsync("SiteMetaKeywords") ?? "",
                ["SiteLogoPath"] = await _settings.GetAsync("SiteLogoPath") ?? "/uploads/Main/160×100.png",
                ["SiteFaviconPath"] = await _settings.GetAsync("SiteFaviconPath") ?? "/favicon.ico"
            };

            return View("Default", model);
        }
    }
}
