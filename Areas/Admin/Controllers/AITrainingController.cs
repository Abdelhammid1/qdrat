using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.AI;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]

    public class AITrainingController : Controller
    {
      

        [HttpPost]
        public IActionResult TrainBatchTeachingModel()
        {
            try
            {
                var result = TeachingInsightTrainer.Train();
                return Content(result); // يتم عرضه في JavaScript
            }
            catch (Exception ex)
            {
                return Content("❌ فشل: " + ex.Message);
            }
        }


    }
}
