using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.AI;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Curriculum;
using System.Linq;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CurriculumsController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;

        public CurriculumsController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }
        // ✅ استخراج تقرير الذكاء الاصطناعي للمناهج

        // ✅ استخراج تقرير الذكاء الاصطناعي للمناهج
        public IActionResult AIAnalysis(int id)
        {
            var curriculum = _context.Curriculums
                .Include(c => c.Sections)
                .Include(c => c.CourseCurriculums)
                    .ThenInclude(cc => cc.Course) // ✅ جلب الدورات المرتبطة بالمنهج
                .FirstOrDefault(c => c.Id == id);

            if (curriculum == null)
                return NotFound();

            var studentPerformances = _context.StudentPerformances
                .Where(sp => sp.CurriculumId == id)
                .ToList();

            var report = AICurriculumAnalyzer.AnalyzeCurriculum(curriculum, studentPerformances);

            return View(report);
        }


        // ✅ عرض جميع المناهج
        public IActionResult Index()
        {
            var curriculums = _context.Curriculums
     .Select(c => new CurriculumViewModel
     {
         Id = c.Id,
         Title = c.Title,
         Description = c.Description,

         // ناخد أول دورة مرتبطة بالمنهج (لو في أكتر من واحدة)
         CourseName = c.CourseCurriculums
                      .Select(cc => cc.Course.Name)
                      .FirstOrDefault(),

         CourseIds = c.CourseCurriculums
               .Select(cc => cc.CourseId)
               .ToList(),

     })
     .ToList();


            return View(curriculums);
        }



        [HttpGet]
        [AdminPermission("Curriculums", "FilterList")]
        public async Task<IActionResult> GetForFilter()
        {
            var data = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title
                })
                .ToListAsync();

            return Json(data);
        }



        // ✅ عرض صفحة إضافة منهج جديد
        // ✅ عرض صفحة إضافة منهج جديد
        public IActionResult Create()
        {
            var model = new CurriculumViewModel
            {
                IsRTL = true, // ✅ default عربي

                Courses = _context.Courses
                                  .Select(c => new SelectListItem
                                  {
                                      Value = c.Id.ToString(),
                                      Text = c.Name
                                  })
                                  .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CurriculumViewModel model)
        {
            ModelState.Remove("CourseNames");
            ModelState.Remove("Modules");
            ModelState.Remove("Courses");

            if (!ModelState.IsValid)
            {
                // إعادة تحميل الدورات لو حصل خطأ
                model.Courses = _context.Courses
                                        .Select(c => new SelectListItem
                                        {
                                            Value = c.Id.ToString(),
                                            Text = c.Name
                                        }).ToList();

                return View(model);
            }

            // إنشاء المنهج
            var curriculum = new Curriculum
            {
                Title = model.Title,
                Description = model.Description,
                CurriculumTypeName = model.CurriculumTypeName,
                IsQuantitative = model.IsQuantitative,
                IsRTL = model.IsRTL, // ✅ NEW
                CreatedAt = DateTime.UtcNow
            };

            _context.Curriculums.Add(curriculum);
            _context.SaveChanges();
            _cache.Remove("CurriculumsCache");

            // ربط المنهج بالدورات المختارة
            if (model.CourseIds != null && model.CourseIds.Any())
            {
                foreach (var courseId in model.CourseIds)
                {
                    _context.CourseCurriculums.Add(new CourseCurriculum
                    {
                        CourseId = courseId,
                        CurriculumId = curriculum.Id
                    });
                }
                _context.SaveChanges();
                _cache.Remove("CurriculumsCache");

            }

            return RedirectToAction(nameof(Index));
        }


        // ✅ عرض تفاصيل المنهج
        public IActionResult Details(int id)
        {
            var curriculum = _context.Curriculums
     .Include(c => c.Modules) // ✅ جلب الوحدات التعليمية
     .Include(c => c.CourseCurriculums)
         .ThenInclude(cc => cc.Course) // ✅ جلب الدورات المرتبطة
     .Where(c => c.Id == id)
     .Select(c => new CurriculumViewModel
     {
         Id = c.Id,
         Title = c.Title,
         Description = c.Description,

         // ✅ جلب أول CourseId مرتبط (لو في أكثر من دورة، ممكن تخزن أول واحدة)
         CourseIds = c.CourseCurriculums
               .Select(cc => cc.CourseId)
               .ToList(),


         // ✅ تحميل قائمة الدورات المرتبطة (عادة بيكون واحدة بس في السيناريو بتاعك)
         Courses = c.CourseCurriculums
                    .Select(cc => new SelectListItem
                    {
                        Value = cc.Course.Id.ToString(),
                        Text = cc.Course.Name
                    }).ToList(),

         Modules = c.Modules.Select(m => new CurriculumModuleViewModel
         {
             Id = m.Id,
             Title = m.Title,
             Content = m.Content,
             CurriculumId = m.CurriculumId
         }).ToList()
     })
     .FirstOrDefault();


            if (curriculum == null)
                return NotFound();

            return View(curriculum);
        }


        // ✅ عرض صفحة تعديل منهج
        // ✅ عرض صفحة تعديل منهج
        // ✅ عرض صفحة تعديل منهج
        public IActionResult Edit(int id)
        {
            var curriculum = _context.Curriculums
                                     .Include(c => c.CourseCurriculums)
                                     .FirstOrDefault(c => c.Id == id);

            if (curriculum == null)
                return NotFound();

            var model = new CurriculumViewModel
            {
                Id = curriculum.Id,
                Title = curriculum.Title,
                Description = curriculum.Description,
                IsQuantitative = curriculum.IsQuantitative,
                CurriculumTypeName = curriculum.CurriculumTypeName,
                IsRTL = curriculum.IsRTL, // ✅ NEW
                CourseIds = curriculum.CourseCurriculums.Select(cc => cc.CourseId).ToList(), // ✅ الدورات المرتبطة
                Courses = _context.Courses
                                  .Select(c => new SelectListItem
                                  {
                                      Value = c.Id.ToString(),
                                      Text = c.Name
                                  })
                                  .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, CurriculumViewModel model)
        {
            ModelState.Remove("CourseNames");
            ModelState.Remove("Modules");
            ModelState.Remove("Courses");

            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                // إعادة تحميل الدورات لو حصل خطأ
                model.Courses = _context.Courses
                                        .Select(c => new SelectListItem
                                        {
                                            Value = c.Id.ToString(),
                                            Text = c.Name
                                        }).ToList();

                return View(model);
            }

            var curriculum = _context.Curriculums
                                     .Include(c => c.CourseCurriculums)
                                     .FirstOrDefault(c => c.Id == id);

            if (curriculum == null)
                return NotFound();

            // تحديث الخصائص الأساسية
            curriculum.Title = model.Title;
            curriculum.Description = model.Description;
            curriculum.CurriculumTypeName = model.CurriculumTypeName;
            curriculum.IsRTL = model.IsRTL; // ✅ NEW
            curriculum.IsQuantitative = model.IsQuantitative;

            _context.Update(curriculum);
            _context.SaveChanges();
            _cache.Remove("CurriculumsCache");

            // تحديث الروابط Many-to-Many
            var oldLinks = _context.CourseCurriculums
                                   .Where(cc => cc.CurriculumId == curriculum.Id)
                                   .ToList();
            _context.CourseCurriculums.RemoveRange(oldLinks);

            if (model.CourseIds != null && model.CourseIds.Any())
            {
                foreach (var courseId in model.CourseIds)
                {
                    _context.CourseCurriculums.Add(new CourseCurriculum
                    {
                        CourseId = courseId,
                        CurriculumId = curriculum.Id
                    });
                }
            }

            _context.SaveChanges();
            _cache.Remove("CurriculumsCache");

            return RedirectToAction(nameof(Index));
        }


        // ✅ حذف المنهج
        public IActionResult Delete(int id)
        {
            var curriculum = _context.Curriculums.Find(id);
            if (curriculum == null)
                return NotFound();

            _context.Curriculums.Remove(curriculum);
            _context.SaveChanges();
            _cache.Remove("CurriculumsCache");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public JsonResult GetCurriculumType(int curriculumId)
        {
            var curriculum = _context.Curriculums
                .Where(c => c.Id == curriculumId)
                .Select(c => new
                {
                    isQuantitative = c.IsQuantitative
                })
                .FirstOrDefault();

            if (curriculum == null)
                return Json(new { isQuantitative = false });

            return Json(curriculum);
        }



        // =========================
        // API – Curriculums For Filters
        // =========================
     
        public class AICurriculumImprover
        {
            public static string ImproveCurriculum(List<StudentPerformance> performances)
            {
                var averageScore = performances.Average(p => p.Score);

                if (averageScore < 60)
                    return "يجب تحسين المحتوى ليكون أكثر وضوحًا وزيادة الأمثلة العملية.";

                if (averageScore > 85)
                    return "المحتوى جيد، ولكن يمكن إضافة تحديات متقدمة.";

                return "المحتوى متوازن ويحتاج لمتابعة.";
            }
        }




    }
}
