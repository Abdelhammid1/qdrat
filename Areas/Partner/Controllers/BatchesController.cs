using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Linq;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class BatchesController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;

        public BatchesController(
            ApplicationDbContext context,
            IPartnerSubscriptionService subscriptionService)
            : base(subscriptionService, context)
        {
            _context = context;
        }

        // =========================================
        // 📋 عرض الدفعات (مع دعم الفلترة حسب الدورة)
        // =========================================
        public IActionResult Index(int? courseId)
        {
            var query = _context.Batches
                .AsNoTracking()
                .Where(b => b.Branch.PartnerId == ActivePartnerId);

            // ✅ فلترة حسب الدورة إذا تم تمريرها
            if (courseId.HasValue)
            {
                query = query.Where(b => b.CourseId == courseId.Value);
            }

            var batches = query
                .OrderByDescending(b => b.Id)
                .Select(b => new PartnerBatchListViewModel
                {
                    Id = b.Id,
                    Name = b.Name,
                    CourseName = b.Course.Name
                })
                .ToList();

            return View(batches);
        }

        // =========================================
        // ➕ إنشاء دفعة
        // =========================================
        [HttpGet]
        public IActionResult Create()
        {
            var model = new CreatePartnerBatchViewModel
            {
                Courses = AllowedCourses
                    .Select(c => new SelectListItem
                    {
                        Value = c.CourseId.ToString(),
                        Text = c.CourseName
                    })
                    .OrderBy(x => x.Text)
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreatePartnerBatchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Courses = AllowedCourses
                    .Select(c => new SelectListItem
                    {
                        Value = c.CourseId.ToString(),
                        Text = c.CourseName
                    })
                    .ToList();

                return View(model);
            }

            if (!AllowedCourses.Any(c => c.CourseId == model.CourseId))
            {
                ModelState.AddModelError("", "الدورة غير مسموح بها في العقد.");
                return View(model);
            }

            var branch = _context.Branches
                .FirstOrDefault(b => b.PartnerId == ActivePartnerId);

            if (branch == null)
            {
                ModelState.AddModelError("", "لا يوجد فرع مرتبط بالشريك.");
                return View(model);
            }

            var batch = new Batch
            {
                Name = model.Name,
                CourseId = model.CourseId,
                BranchId = branch.Id
            };

            _context.Batches.Add(batch);
            _context.SaveChanges();

            TempData["Success"] = "تم إنشاء الدفعة بنجاح.";
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }

        // =========================================
        // ✏️ تعديل دفعة
        // =========================================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var batch = _context.Batches
                .AsNoTracking()
                .Include(b => b.Branch)
                .FirstOrDefault(b =>
                    b.Id == id &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
                return NotFound();

            var model = new EditPartnerBatchViewModel
            {
                Id = batch.Id,
                Name = batch.Name,
                CourseId = batch.CourseId,
                Courses = AllowedCourses
                    .Select(c => new SelectListItem
                    {
                        Value = c.CourseId.ToString(),
                        Text = c.CourseName
                    })
                    .ToList()
            };

            return View(model);
        }


        // =========================================
        // 📄 تفاصيل الدفعة
        // =========================================
        public IActionResult Details(int id)
        {
            var batch = _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Include(b => b.Branch)
                .FirstOrDefault(b =>
                    b.Id == id &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
                return NotFound();

            var students = _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == id)
                .Select(e => new PartnerBatchStudentItemViewModel
                {
                    StudentId = e.Student.StudentID,
                    FullName = e.Student.FullName,
                    NationalId = e.Student.NationalID,
                    Phone = e.Student.PhoneNumber,
                    Level = e.Student.Level
                })
                .ToList();

            var model = new PartnerBatchDetailsViewModel
            {
                BatchId = batch.Id, // ✅ أهم سطر
                BatchName = batch.Name,
                CourseName = batch.Course.Name,
                StudentsCount = students.Count,
                Students = students
            };

            return View(model);
        }

        // =========================================
        // 🔍 البحث داخل طلاب الدفعة
        // =========================================
        [HttpGet]
        public IActionResult SearchBatchStudents(int batchId, string term)
        {
            // ===============================
            // 1️⃣ جلب التسجيلات
            // ===============================
            var enrollments = _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .ToList();

            var studentIds = enrollments
                .Select(x => x.StudentID)
                .ToList();

            // ===============================
            // 2️⃣ جلب كل الطلاب
            // ===============================
            var allStudents = _context.Students
                .AsNoTracking()
                .ToList();

            // ===============================
            // 3️⃣ فلترة بالدفعة
            // ===============================
            var students = allStudents
                .Where(s => studentIds.Any(id => id == s.StudentID))
                .ToList();

            // ===============================
            // 4️⃣ فلترة البحث (في الذاكرة)
            // ===============================
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                students = students
                    .Where(s =>
                        (!string.IsNullOrEmpty(s.FullName) && s.FullName.Contains(term)) ||
                        (!string.IsNullOrEmpty(s.NationalID) && s.NationalID.Contains(term))
                    )
                    .ToList();
            }

            // ===============================
            // 5️⃣ تحويل النتيجة
            // ===============================
            var result = students
                .Select(s => new
                {
                    studentId = s.StudentID,
                    fullName = s.FullName,
                    nationalId = s.NationalID,
                    phone = s.PhoneNumber,
                    level = s.Level
                })
                .ToList();

            return Json(result);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditPartnerBatchViewModel model)
        {
            var batch = _context.Batches
                .Include(b => b.Branch)
                .FirstOrDefault(b =>
                    b.Id == model.Id &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
                return NotFound();

            if (!AllowedCourses.Any(c => c.CourseId == model.CourseId))
            {
                ModelState.AddModelError("", "الدورة غير مسموح بها في العقد.");
                return View(model);
            }

            batch.Name = model.Name;
            batch.CourseId = model.CourseId;

            _context.SaveChanges();

            TempData["Success"] = "تم تعديل الدفعة بنجاح.";
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }
    }
}
