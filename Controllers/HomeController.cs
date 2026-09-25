using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Models;
using QdratNew.Services.Frontend.HomePage;

namespace QdratNew.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHomePageContentService _homePageContent;

    public HomeController(ILogger<HomeController> logger, IHomePageContentService homePageContent)
    {
        _logger = logger;
        _homePageContent = homePageContent;
    }

    public async Task<IActionResult> Index()
    {
        var model = await _homePageContent.GetHomeContentAsync();
        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
