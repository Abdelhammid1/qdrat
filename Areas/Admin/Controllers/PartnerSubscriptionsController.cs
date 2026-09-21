using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.PartnerSubscriptions;
using System;
using System.Linq;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class PartnerSubscriptionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PartnerSubscriptionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // 📄 INDEX – عرض عقود مدرسة واحدة
        // =====================================================
        public IActionResult Index(int partnerId)
        {
            if (partnerId <= 0)
                return BadRequest();

            var partner = _context.Partners
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == partnerId);

            if (partner == null)
                return NotFound();

            var subscriptions = _context.PartnerSubscriptions
                .Where(s => s.PartnerId == partnerId)
                .OrderByDescending(s => s.StartDate)
                .Select(s => new PartnerSubscriptionListItemViewModel
                {
                    Id = s.Id,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    MaxActiveStudents = s.MaxActiveStudents,
                    IsActive = s.IsActive,
                    AccessUntilDate = s.AccessUntilDate,
                    CoursesCount = s.SubscriptionCourses.Count()

                })
                .ToList();

            return View(new PartnerSubscriptionIndexViewModel
            {
                PartnerId = partner.Id,
                PartnerName = partner.Name,
                Subscriptions = subscriptions
            });
        }




        // =====================================================
        // ➕ CREATE – GET
        // =====================================================
        public IActionResult Create(int partnerId)
        {
            var partner = _context.Partners.Find(partnerId);
            if (partner == null)
                return NotFound();

            var now = DateTime.Now;

            bool hasActive = _context.PartnerSubscriptions
                .Any(s => s.PartnerId == partnerId &&
                          s.StartDate <= now &&
                          s.EndDate >= now);

            if (hasActive)
            {
                TempData["Error"] = "لا يمكن إنشاء عقد جديد لوجود عقد نشط بالفعل.";
                return RedirectToAction(nameof(Index), new { partnerId });
            }

            var courses = _context.Courses
                .OrderBy(c => c.Name)
                .Select(c => new PartnerSubscriptionCourseVM
                {
                    CourseId = c.Id,
                    CourseName = c.Name,
                    CanUsePlatformQuestionBank = true
                })
                .ToList();

            return View(new PartnerSubscriptionCreateViewModel
            {
                PartnerId = partner.Id,
                PartnerName = partner.Name,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),

                Courses = courses
            });
        }

        // =====================================================
        // ➕ CREATE – POST
        // =====================================================
        // =====================================================
        // ➕ CREATE – POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PartnerSubscriptionCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);


            if (!model.MaxInstructors.HasValue)
            {
                ModelState.AddModelError("MaxInstructors", "يجب تحديد الحد الأقصى للمدربين.");
                return View(model);
            }




            if (model.Courses == null || !model.Courses.Any(x => x.IsSelected))
            {
                ModelState.AddModelError("", "يجب اختيار دورة واحدة على الأقل.");
                return View(model);
            }

            var now = DateTime.Now;

            bool hasActive = _context.PartnerSubscriptions
                .Any(s => s.PartnerId == model.PartnerId &&
                          s.StartDate <= now &&
                          s.EndDate >= now);

            if (hasActive)
            {
                ModelState.AddModelError("", "يوجد عقد نشط بالفعل لهذه المدرسة.");
                return View(model);
            }

            // ============================
            // 1️⃣ إنشاء عقد الشراكة
            // ============================
            var subscription = new PartnerSubscription
            {
                PartnerId = model.PartnerId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxActiveStudents = model.MaxActiveStudents,

                AllowMultipleBranches = model.AllowMultipleBranches,
                MaxInstructors = model.MaxInstructors,
                CanCreateHomework = model.CanCreateHomework,
                CanCreateExams = model.CanCreateExams,
                CanUsePlacementExams = model.CanUsePlacementExams,
                CanUsePerformanceIndicatorExams = model.CanUsePerformanceIndicatorExams,
                CanUseReinforcementSkills = model.CanUseReinforcementSkills,
                CanUseRemedialPlans = model.CanUseRemedialPlans,
                CanUseRemedialSessions = model.CanUseRemedialSessions,
                CanAccessEducationalContent = model.CanAccessEducationalContent,
                AccessUntilDate = model.AccessUntilDate,
                CanUseProfessionalModels = model.CanUseProfessionalModels
            };

            _context.PartnerSubscriptions.Add(subscription);
            _context.SaveChanges(); // 🔴 مهم للحصول على SubscriptionId

            // ============================
            // 2️⃣ إنشاء الفترة الأولى تلقائيًا (مغذاة من العقد)
            // ============================
            var initialPeriod = new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = subscription.Id,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                MaxStudents = subscription.MaxActiveStudents
            };

            _context.PartnerSubscriptionPeriods.Add(initialPeriod);

            // ============================
            // 3️⃣ ربط الدورات
            // ============================
            foreach (var c in model.Courses.Where(x => x.IsSelected))
            {
                _context.PartnerSubscriptionCourses.Add(new PartnerSubscriptionCourse
                {
                    PartnerSubscriptionId = subscription.Id,
                    CourseId = c.CourseId,
                    CanUsePlatformQuestionBank = c.CanUsePlatformQuestionBank
                });
            }

            _context.SaveChanges();

            TempData["Success"] = "تم إنشاء عقد الشراكة والفترة الأولى تلقائيًا بنجاح.";
            return RedirectToAction(nameof(Index), new { partnerId = model.PartnerId });
        }


        // =====================================================
        // ✏️ EDIT – GET
        // =====================================================
        public IActionResult Edit(int id)
        {
            var subscription = _context.PartnerSubscriptions
                .Include(s => s.Partner)
                .Include(s => s.SubscriptionCourses)
                .FirstOrDefault(s => s.Id == id);

            if (subscription == null)
                return NotFound();

            if (!subscription.IsActive)
            {
                TempData["Error"] = "لا يمكن تعديل عقد منتهي.";
                return RedirectToAction(nameof(Index), new { partnerId = subscription.PartnerId });
            }

            var selected = subscription.SubscriptionCourses
                .ToDictionary(x => x.CourseId);

            var courses = _context.Courses
                .OrderBy(c => c.Name)
                .Select(c => new PartnerSubscriptionCourseVM
                {
                    CourseId = c.Id,
                    CourseName = c.Name,
                    IsSelected = selected.ContainsKey(c.Id),
                    CanUsePlatformQuestionBank = selected.ContainsKey(c.Id)
                        ? selected[c.Id].CanUsePlatformQuestionBank
                        : true
                })
                .ToList();

            return View(new PartnerSubscriptionEditViewModel
            {
                Id = subscription.Id,
                PartnerId = subscription.PartnerId,
                PartnerName = subscription.Partner.Name,
                MaxInstructors = subscription.MaxInstructors,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                MaxActiveStudents = subscription.MaxActiveStudents,

                AllowMultipleBranches = subscription.AllowMultipleBranches,

                CanCreateHomework = subscription.CanCreateHomework,
                CanCreateExams = subscription.CanCreateExams,
                CanUsePlacementExams = subscription.CanUsePlacementExams,
                CanUsePerformanceIndicatorExams = subscription.CanUsePerformanceIndicatorExams,
                CanUseReinforcementSkills = subscription.CanUseReinforcementSkills,
                CanUseRemedialPlans = subscription.CanUseRemedialPlans,
                CanUseRemedialSessions = subscription.CanUseRemedialSessions,
                CanAccessEducationalContent = subscription.CanAccessEducationalContent,
                AccessUntilDate = subscription.AccessUntilDate,
                CanUseProfessionalModels = subscription.CanUseProfessionalModels,

                Courses = courses
            });
        }

        // =====================================================
        // ✏️ EDIT – POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(PartnerSubscriptionEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.Courses == null || !model.Courses.Any(x => x.IsSelected))
            {
                ModelState.AddModelError("", "يجب اختيار دورة واحدة على الأقل.");
                return View(model);
            }

            var subscription = _context.PartnerSubscriptions
                .Include(s => s.SubscriptionCourses)
                .FirstOrDefault(s => s.Id == model.Id);

            if (subscription == null)
                return NotFound();

            if (!subscription.IsActive)
            {
                TempData["Error"] = "لا يمكن تعديل عقد منتهي.";
                return RedirectToAction(nameof(Index), new { partnerId = subscription.PartnerId });
            }

            subscription.EndDate = model.EndDate;
            subscription.MaxActiveStudents = model.MaxActiveStudents;
            subscription.AllowMultipleBranches = model.AllowMultipleBranches;

            subscription.CanCreateHomework = model.CanCreateHomework;
            subscription.CanCreateExams = model.CanCreateExams;
            subscription.CanUsePlacementExams = model.CanUsePlacementExams;
            subscription.CanUsePerformanceIndicatorExams = model.CanUsePerformanceIndicatorExams;
            subscription.CanUseReinforcementSkills = model.CanUseReinforcementSkills;
            subscription.CanUseRemedialPlans = model.CanUseRemedialPlans;
            subscription.CanUseRemedialSessions = model.CanUseRemedialSessions;
            subscription.CanAccessEducationalContent = model.CanAccessEducationalContent;
            subscription.AccessUntilDate = model.AccessUntilDate;
            subscription.CanUseProfessionalModels = model.CanUseProfessionalModels;
            subscription.MaxInstructors = model.MaxInstructors;
            subscription.SubscriptionCourses.Clear();

            foreach (var c in model.Courses.Where(x => x.IsSelected))
            {
                subscription.SubscriptionCourses.Add(new PartnerSubscriptionCourse
                {
                    CourseId = c.CourseId,
                    CanUsePlatformQuestionBank = c.CanUsePlatformQuestionBank
                });
            }

            _context.SaveChanges();

            TempData["Success"] = "تم تحديث عقد الشراكة بنجاح.";
            return RedirectToAction(nameof(Index), new { partnerId = subscription.PartnerId });
        }




        // =====================================================
        // 🔍 DETAILS – عرض تفاصيل العقد
        // =====================================================
        public IActionResult Details(int id)
        {
            var subscription = _context.PartnerSubscriptions
                .Include(s => s.Partner)
                .FirstOrDefault(s => s.Id == id);

            if (subscription == null)
                return NotFound();

            var instructorsCount = _context.Instructors
                .Count(x =>
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            var model = new PartnerSubscriptionDetailsViewModel
            {
                PartnerName = subscription.Partner.Name,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                MaxActiveStudents = subscription.MaxActiveStudents,
                MaxInstructors = subscription.MaxInstructors,
                CurrentInstructorsCount = instructorsCount, // 🔴 مهم
                AllowMultipleBranches = subscription.AllowMultipleBranches,
                CanUseQuestionBank = subscription.CanUseQuestionBank,
                CanCreateHomework = subscription.CanCreateHomework,
                CanCreateExams = subscription.CanCreateExams,
                CanUsePlacementExams = subscription.CanUsePlacementExams,
                CanUsePerformanceIndicatorExams = subscription.CanUsePerformanceIndicatorExams,
                CanUseReinforcementSkills = subscription.CanUseReinforcementSkills,
                CanUseRemedialPlans = subscription.CanUseRemedialPlans,
                CanUseRemedialSessions = subscription.CanUseRemedialSessions,
                CanUseProfessionalModels = subscription.CanUseProfessionalModels,
                CanAccessEducationalContent = subscription.CanAccessEducationalContent,
                AccessUntilDate = subscription.AccessUntilDate
            };

            return View(model);
        }


    }
}
