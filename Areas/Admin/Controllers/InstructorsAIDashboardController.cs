using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.AI;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;
using QdratNew.ViewModels.Section;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstructorsAIDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstructorsAIDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? curriculumId, int? batchId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.StudentPerformances
                .Include(p => p.Student)
                .Include(p => p.Curriculum)
                    .ThenInclude(c => c.CurriculumInstructors)
                        .ThenInclude(ci => ci.Instructor)
                .AsQueryable();

            if (curriculumId.HasValue)
                query = query.Where(p => p.CurriculumId == curriculumId.Value);

            if (batchId.HasValue)
            {
                query = query.Where(p =>
                    _context.StudentBatchEnrollments
                        .Any(e => e.StudentID == p.Student.StudentID && e.BatchId == batchId.Value)
                );
            }


            if (fromDate.HasValue)
                query = query.Where(p => p.ExamDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.ExamDate <= toDate.Value);

            var data = await query.ToListAsync();

            // ✅ تحليل الإحصائيات للجدول
            var instructorsStats = data
                .Where(p => p.Curriculum?.CurriculumInstructors != null)
                .SelectMany(p => p.Curriculum.CurriculumInstructors.Select(ci => new
                {
                    ci.Instructor,
                    p.Score,
                    StudentId = p.StudentID,
                    p.CurriculumId
                }))
                .GroupBy(x => x.Instructor)
                .Select(g =>
                {
                    var avgScore = Math.Round(g.Average(x => x.Score), 2);
                    var successRate = Math.Round(g.Count(x => x.Score >= 60) * 100.0 / g.Count(), 2);
                    var totalStudents = g.Select(x => x.StudentId).Distinct().Count();
                    var totalCurriculums = g.Select(x => x.CurriculumId).Distinct().Count();

                    return new InstructorPerformanceAIViewModel
                    {
                        InstructorId = g.Key.Id,
                        InstructorName = g.Key.FullName,
                        TotalCurriculums = totalCurriculums,
                        AverageStudentScore = avgScore,
                        SuccessRate = successRate,
                        TotalStudents = totalStudents,
                        AIRecommendations = GenerateRecommendations(avgScore, successRate)

                    };
                })
                .OrderByDescending(i => i.SuccessRate)
                .ToList();

            // استعلام أداء المدرب على مستوى المحاور التعليمية
            var sectionPerformance =
        await (
            from sp in _context.StudentPerformances
                // فلترة أساسية
            where sp.SectionId != null
                  // فلترة الدفعة عبر جدول الربط (بدل sp.Student.BatchId)
                  && (!batchId.HasValue
                      || _context.StudentBatchEnrollments.Any(e =>
                            e.StudentID == sp.StudentID && e.BatchId == batchId.Value))

            // انضمام اختياري للحصول على عنوان السيكشن
            join sec in _context.Sections on sp.SectionId equals sec.Id into JSec
            from sec in JSec.DefaultIfEmpty()

                // انضمام لاستخراج اسم المدرّس من CurriculumInstructors
            join ci in _context.CurriculumInstructors on sp.CurriculumId equals ci.CurriculumId into JCI
            from ci in JCI.DefaultIfEmpty()

            join ins in _context.Instructors on ci.InstructorId equals ins.Id into JIns
            from ins in JIns.DefaultIfEmpty()

            group sp by new
            {
                SectionTitle = (sec != null ? sec.Title : "غير محدد"),
                InstructorName = (ins != null ? ins.FullName : "غير محدد")
            }
            into g
            select new SectionPerformanceAIViewModel
            {
                InstructorName = g.Key.InstructorName,
                SectionTitle = g.Key.SectionTitle,
                AverageScore = g.Average(x => x.Score),
                SuccessRate = g.Count(x => x.Score >= 60) * 100.0 / g.Count()
            }
        ).ToListAsync();





            // ✅ تعبئة البيانات الخاصة بالرسم البياني
            var chartData = data
                .Where(p => p.Curriculum?.CurriculumInstructors != null)
                .SelectMany(p => p.Curriculum.CurriculumInstructors.Select(ci => new
                {

                    InstructorName = ci.Instructor.FullName,
                    p.Score
                }))
                .GroupBy(x => x.InstructorName)
                .Select(g => new InstructorPerformanceChartViewModel
                {
                    InstructorName = g.Key,
                    AverageStudentScore = g.Average(x => x.Score)
                })
                .ToList();

            // ✅ تحميل القوائم المنسدلة
            var curriculumList = await _context.Curriculums
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            var batchList = await _context.Batches
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToListAsync();

            // ✅ تعبئة ViewModel واحد فقط بكل شيء
            var vm = new InstructorsAIDashboardViewModel
            {
                InstructorsStats = instructorsStats,
                CurriculumList = curriculumList,
                SelectedCurriculumId = curriculumId,
                BatchList = batchList,
                SelectedBatchId = batchId,
                FromDate = fromDate,
                ToDate = toDate,

                InstructorPerformanceChartData = chartData
            };

            // ✅ تحليل المحاور التعليمية لكل مدرب
            vm.SectionPerformances = sectionPerformance;

            // ✅ Debug لإظهار البيانات في نافذة الإخراج
            System.Diagnostics.Debug.WriteLine("عدد بيانات الرسم البياني: " + chartData.Count);
            foreach (var item in chartData)
            {
                System.Diagnostics.Debug.WriteLine($"مدرب: {item.InstructorName}, متوسط: {item.AverageStudentScore}");
            }
            return View(vm);
        }



        public async Task<IActionResult> SectionPerformance(int instructorId, int? batchId)
        {
            // ✅ جلب اسم المدرب من قاعدة البيانات
            var instructorName = await _context.Instructors
                .Where(i => i.Id == instructorId)
                .Select(i => i.FullName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(instructorName))
                return NotFound("لم يتم العثور على المدرب المطلوب.");

            // ✅ جلب الأداء بحسب المحاور التعليمية والمدرب
            var performances = await _context.StudentPerformances
                .Include(sp => sp.Section)
                .Include(sp => sp.Curriculum)
                    .ThenInclude(c => c.CurriculumInstructors)
                .Include(sp => sp.Student)
                .Where(sp =>
                    sp.SectionId != null &&
                    sp.Curriculum.CurriculumInstructors.Any(ci => ci.InstructorId == instructorId)
                )
                .ToListAsync();

            // ✅ فلترة الأداء حسب الدفعة إن وُجدت
            if (batchId.HasValue)
            {
                performances = performances
                    .Where(sp => _context.StudentBatchEnrollments
                        .Any(e => e.StudentID == sp.Student.StudentID && e.BatchId == batchId.Value))
                    .ToList();
            }

            // ✅ تحليل الأداء لكل محور تعليمي
            var grouped = performances
                .GroupBy(sp => sp.Section.Title)
                .Select(g => new SectionPerformanceAIViewModel
                {
                    SectionTitle = g.Key,
                    AverageScore = Math.Round(g.Average(x => x.Score), 2),
                    SuccessRate = Math.Round(g.Count(x => x.Score >= 60) * 100.0 / g.Count(), 1)
                })
                .ToList();

            // ✅ تحميل قائمة الدفعات
            var batchList = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            // ✅ إنشاء ViewModel النهائي
            var vm = new SectionPerformancePerInstructorViewModel
            {
                InstructorId = instructorId,
                InstructorName = instructorName,
                Sections = grouped,
                BatchList = batchList,
                SelectedBatchId = batchId
            };

            return View(vm);
        }





        private List<string> GenerateRecommendations(double avgScore, double successRate)
        {
            var recommendations = new List<string>();

            if (successRate >= 90 && avgScore >= 80)
            {
                recommendations.Add("أداء ممتاز – يُوصى بتكليفه بتدريب مدربين آخرين");
                recommendations.Add("اقتراح تطوير محتوى متقدم بناءً على تجربته");
            }
            else if (successRate >= 75 && avgScore >= 65)
            {
                recommendations.Add("أداء جيد – يُوصى بإشراكه في تطوير المناهج الحالية");
            }
            else if (successRate >= 60)
            {
                recommendations.Add("أداء متوسط – يُوصى بإعادة تأهيله عبر برامج تدريبية");
            }
            else
            {
                recommendations.Add("أداء منخفض – يُوصى بمراجعة أسلوب الشرح");
                recommendations.Add("تقديم دعم إضافي وتدريب تعزيزي");
            }

            return recommendations;
        }



    }
}
