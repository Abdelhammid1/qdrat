using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;
using QdratNew.ViewModels.Section;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstructorsAISectionEvaluationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstructorsAISectionEvaluationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int instructorId, int? batchId, int? curriculumId, DateTime? fromDate, DateTime? toDate)
        {
            // ✅ جلب اسم المدرب
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null)
                return NotFound("لم يتم العثور على المدرب.");

            // ✅ تحميل قائمة الدفعات مع تحديد العنصر المختار
            var batchList = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name,
                    Selected = batchId.HasValue && b.Id == batchId.Value
                }).ToListAsync();

            // ✅ تحميل قائمة المناهج مع تحديد العنصر المختار
            var curriculumList = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = curriculumId.HasValue && c.Id == curriculumId.Value
                }).ToListAsync();

            // ✅ جلب الأداء حسب الشروط
            var performances = await _context.StudentPerformances
                .Include(sp => sp.Student)
                .Include(sp => sp.Section)
                .Include(sp => sp.Curriculum)
                .Where(sp =>
                    sp.Curriculum.CurriculumInstructors.Any(ci => ci.InstructorId == instructorId)
                    && sp.SectionId != null
                )
                .ToListAsync();

            if (batchId.HasValue)
            {
                performances = performances
                    .Where(sp => _context.StudentBatchEnrollments
                        .Any(e => e.StudentID == sp.Student.StudentID && e.BatchId == batchId.Value))
                    .ToList();
            }

            if (curriculumId.HasValue)
                performances = performances.Where(sp => sp.CurriculumId == curriculumId.Value).ToList();

            if (fromDate.HasValue)
                performances = performances.Where(sp => sp.ExamDate >= fromDate.Value).ToList();

            if (toDate.HasValue)
                performances = performances.Where(sp => sp.ExamDate <= toDate.Value).ToList();

            // ✅ تحليل أداء المحاور
            var sectionEvaluations = performances
                .GroupBy(sp => sp.Section.Title)
                .Select(g => new SectionEvaluationAIViewModel
                {
                    SectionTitle = g.Key,
                    AverageScore = Math.Round(g.Average(sp => sp.Score), 2),
                    SuccessRate = Math.Round(g.Count(sp => sp.Score >= 60) * 100.0 / g.Count(), 1),
                    StudentCount = g.Count(),
                    DifficultyLevel = g.Average(sp => sp.Score) < 50 ? "صعب" :
                                      g.Average(sp => sp.Score) < 70 ? "متوسط" : "سهل",
                    AIComment = "تحليل تلقائي سيضاف لاحقاً"
                })
                .ToList();

            // ✅ حساب المؤشرات العامة
            double overallScore = sectionEvaluations.Any() ? sectionEvaluations.Average(s => s.AverageScore) : 0;
            double successRate = sectionEvaluations.Any() ? sectionEvaluations.Average(s => s.SuccessRate) : 0;

            // ✅ حساب التفاعل والحضور (اختياري ويمكن تعديله لاحقاً)
            double engagementRate = performances.Any() ? Math.Round(performances.Average(sp => sp.EngagementRate), 2) : 0;
            double attendanceRate = performances.Any() ? Math.Round(performances.Average(sp => sp.AttendanceCount), 2) : 0;


            // ✅ توصيات (ستُبنى لاحقًا عبر الذكاء الاصطناعي)
            var aiRecommendations = new List<string>
    {
        "تقديم تدريب إضافي في المحاور التي بها نسبة نجاح منخفضة.",
        "تشجيع التفاعل عبر أدوات التعليم التفاعلي.",
        "مراجعة محتوى المحاور الصعبة وتحسينها."
    };

            // ✅ إنشاء ViewModel
            var vm = new InstructorSectionEvaluationViewModel
            {
                InstructorId = instructorId,
                InstructorName = instructor.FullName,
                SelectedBatchId = batchId,
                BatchList = batchList,
                SelectedCurriculumId = curriculumId,
                CurriculumList = curriculumList,
                FromDate = fromDate,
                ToDate = toDate,
                SectionsEvaluation = sectionEvaluations,
                AIOverallScore = Math.Round(overallScore, 2),
                AIEvaluationComment = overallScore > 80 ? "أداء مميز" :
                                      overallScore > 60 ? "أداء جيد" :
                                      overallScore > 40 ? "يحتاج تطوير" : "ضعيف جداً",
                TotalSectionsCovered = sectionEvaluations.Count,
                AverageStudentScore = overallScore,
                SuccessRate = successRate,
                EngagementRate = engagementRate,
                AttendanceRate = attendanceRate,
                AIRecommendations = aiRecommendations
            };

            return View(vm);
        }

        // 🔍 دوال التحليل الذكي
        private string GetDifficultyLevel(double score)
        {
            if (score >= 85) return "سهل";
            if (score >= 60) return "متوسط";
            return "صعب";
        }

        private string GetAIComment(double score)
        {
            if (score >= 85) return "أداء متميز في هذا المحور.";
            if (score >= 60) return "أداء جيد يمكن تحسينه.";
            return "الأداء يحتاج إلى تحسين كبير.";
        }

        private string GenerateAIComment(double score, double successRate, double engagement, double attendance)
        {
            if (score >= 80 && successRate >= 80 && engagement >= 70)
                return "المدرب يقدم أداء ممتاز من حيث النتائج والتفاعل.";
            if (score < 60)
                return "المدرب يحتاج لتحسين نتائج الطلاب بشكل ملحوظ.";
            return "الأداء مقبول لكن توجد فرص لتحسين التفاعل والنتائج.";
        }

        private List<string> GenerateRecommendations(double score, double successRate, double engagement, double attendance)
        {
            var list = new List<string>();

            if (score < 60)
                list.Add("تحسين جودة الشرح أو الأسئلة المرتبطة بالمحاور الصعبة.");

            if (successRate < 70)
                list.Add("التركيز على تعزيز الفهم من خلال خطط علاجية.");

            if (engagement < 50)
                list.Add("تشجيع التفاعل داخل المنصة باستخدام أدوات التحفيز.");

            if (attendance < 50)
                list.Add("تحسين نسبة حضور الطلاب من خلال رسائل تذكير.");

            if (list.Count == 0)
                list.Add("استمر بنفس المستوى الممتاز مع تطوير مستمر.");

            return list;
        }
    }
}
