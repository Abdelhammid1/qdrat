using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.ML;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class ChartTrainerController : Controller
    {
        public IActionResult TrainModel()
        {
            var result = ChartTrainer.TrainModel();

            TempData["ModelTrainingResult"] = result;
            return RedirectToAction("Index", "Branches");
        }
    }
}
