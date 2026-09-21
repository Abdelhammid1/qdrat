using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.AI.Analysis;
using QdratNew.ViewModels.AI;
using QdratNew.ViewModels.Shared;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/ChartAI/[action]")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public static class ChartAIAnalyzer
    {
        public static StudentAIAnalysisResult Analyze(ChartAnalysisInputViewModel input)
        {
            var result = new StudentAIAnalysisResult
            {
                AssessedLevel = "متوسط",
                RiskScore = 2,
                Recommendations = new List<string>
        {
            "مراجعة دروس الجبر",
            "التركيز على حل الواجبات القادمة في الوقت المحدد"
        }
            };

            return result;
        }



    }
}