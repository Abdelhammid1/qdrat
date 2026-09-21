// ✅ QdratNew.Controllers.PrintController.cs
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;

namespace QdratNew.Controllers
{
    public class PrintController : Controller
    {
        // Generic PDF print action for any View
        [HttpGet]
        public IActionResult ViewAsPdf(string viewName, string area = "", string controller = "", string id = null)
        {
            var routeValues = new { area = area, controller = controller, action = viewName, id = id };

            return new ViewAsPdf(viewName, routeValues)
            {
                FileName = "Report.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }
    }
}
