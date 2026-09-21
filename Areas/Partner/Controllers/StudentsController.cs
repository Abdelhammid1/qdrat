using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Interfaces;
using QdratNew.Services;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class StudentsController : PartnerBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPartnerSubscriptionService _subscriptionService;

        public StudentsController(
         ApplicationDbContext context,
         UserManager<ApplicationUser> userManager,
         IPartnerSubscriptionService subscriptionService)
         : base(subscriptionService, context)
        {
            _context = context;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
        }


        // =====================================================
        // 📋 طلاب الفترة النشطة
        // =====================================================
        // =====================================================
        // 📋 طلاب الفترة النشطة + البحث
        // =====================================================
        public IActionResult Index(string search = null)
        {
            // 1️⃣ جلب البيانات الأساسية فقط (بدون بحث)
            var students = _context.Students
                .Where(s => s.Branch.PartnerId == ActivePartnerId && s.IsActiveForLearning)
                .Select(s => new StudentListViewModel
                {
                    Id = s.StudentID,
                    FullName = s.FullName,
                    NationalId = s.NationalID,
                    IsActiveForLearning = s.IsActiveForLearning
                })
                .ToList(); // ⬅️ التحويل إلى الذاكرة (مهم جدًا)

            // 2️⃣ تطبيق البحث في الذاكرة فقط
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                students = students
                    .Where(s =>
                        (!string.IsNullOrEmpty(s.FullName) &&
                         s.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                     || (!string.IsNullOrEmpty(s.NationalId) &&
                         s.NationalId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    )
                    .ToList();
            }

            // 3️⃣ الترتيب النهائي
            students = students
                .OrderBy(s => s.FullName)
                .ToList();

            ViewData["Search"] = search;

            return View(students);
        }


        // =====================================================
        // 🔍 بحث طلاب (Fetch – خفيف)
        // =====================================================
        [HttpGet]
        public IActionResult Search(string term)
        {
            var students = _context.Students
                .Where(s => s.Branch.PartnerId == ActivePartnerId && s.IsActiveForLearning)
                .Select(s => new StudentListViewModel
                {
                    Id = s.StudentID,
                    FullName = s.FullName,
                    NationalId = s.NationalID,
                    IsActiveForLearning = s.IsActiveForLearning
                })
                .ToList(); // ⬅️ تحميل مرة واحدة

            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                students = students
                    .Where(s =>
                        (!string.IsNullOrEmpty(s.FullName) &&
                         s.FullName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                     || (!string.IsNullOrEmpty(s.NationalId) &&
                         s.NationalId.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Take(50) // ⬅️ حماية الأداء
                    .ToList();
            }
            else
            {
                students = students.Take(50).ToList();
            }

            return PartialView("_StudentsTablePartial", students);
        }





        [HttpGet]
        public IActionResult ImportExcel()
        {
            var model = new PartnerStudentImportViewModel
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
        public async Task<IActionResult> ImportExcel(
            IFormFile excelFile,
            int courseId,
            int batchId,
            [FromServices] IPartnerStudentImportService importService)
        {
            if (courseId <= 0 || batchId <= 0)
            {
                TempData["Error"] = "يجب اختيار الدورة والدفعة قبل رفع الملف.";
                return RedirectToAction(nameof(ImportExcel));
            }

            var result = await importService.ImportFromExcelAsync(
                excelFile,
                ActivePartnerId,
                ActiveSubscriptionPeriodId,
                courseId,
                batchId
            );

            if (!result.Success)
            {
                TempData["Error"] = string.Join("<br/>", result.Errors);
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"تم استيراد {result.InsertedCount} طالب بنجاح.";
            return RedirectToAction(nameof(Index));
        }




        [HttpGet]
        public IActionResult AddToBatch(int studentId)
        {
            var student = _context.Students
                .FirstOrDefault(s =>
                    s.StudentID == studentId &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            var model = new AddStudentToBatchViewModel
            {
                StudentId = studentId,
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


        [HttpGet]
        public IActionResult GetPartnerBatchesByCourse(int courseId)
        {
            var allowed = AllowedCourses.Any(c => c.CourseId == courseId);
            if (!allowed)
                return Json(new List<object>());

            var batches = _context.Batches
                .Where(b =>
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == ActivePartnerId &&
                    b.IsActive &&
                    !b.IsDeleted)
                .Select(b => new
                {
                    id = b.Id,
                    name = b.Name
                })
                .OrderBy(b => b.name)
                .ToList();

            return Json(batches);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToBatch(AddStudentToBatchViewModel model)
        {
            var student = _context.Students
                .FirstOrDefault(s =>
                    s.StudentID == model.StudentId &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            var batch = _context.Batches
                .Include(b => b.Branch)
                .FirstOrDefault(b =>
                    b.Id == model.BatchId &&
                    b.CourseId == model.CourseId &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
            {
                ModelState.AddModelError("", "الدفعة غير صالحة أو غير تابعة للشريك.");
                return View(model);
            }

            bool alreadyEnrolled = _context.StudentBatchEnrollments
                .Any(e =>
                    e.StudentID == student.StudentID &&
                    e.BatchId == batch.Id);

            if (alreadyEnrolled)
            {
                ModelState.AddModelError("", "الطالب مسجل بالفعل في هذه الدفعة.");
                return View(model);
            }

            _context.StudentBatchEnrollments.Add(new StudentBatchEnrollment
            {
                StudentID = student.StudentID,
                BatchId = batch.Id,
                Status = "Active"
            });

            _context.SaveChanges();

            TempData["Success"] = "تم إلحاق الطالب بالدفعة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = student.StudentID });
        }


        [HttpGet]
        public IActionResult GetBatchesByCourseForImport(int courseId)
        {
            if (courseId <= 0)
                return Json(new List<object>());

            // 🔒 تأكد أن الدورة ضمن العقد
            var allowed = AllowedCourses.Any(c => c.CourseId == courseId);
            if (!allowed)
                return Json(new List<object>());

            var batches = _context.Batches
                .Where(b =>
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == ActivePartnerId
                )
                .Select(b => new
                {
                    id = b.Id,
                    name = b.Name
                })
                .OrderBy(b => b.name)
                .ToList();

            return Json(batches);
        }



        [HttpGet]
        public IActionResult DownloadExcelTemplate(
                 [FromServices] PartnerStudentExcelTemplateService templateService)
        {
            var fileBytes = templateService.GenerateTemplate();

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Partner_Students_Template.xlsx"
            );
        }


        // =====================================================
        // 🗂️ طلاب الأرشيف (فترات سابقة)
        // =====================================================
        public IActionResult Archive()
        {
            var students = _context.Students
                .Where(s =>
                    s.Branch.PartnerId == ActivePartnerId &&
                    !s.IsActiveForLearning)
                .Select(s => new StudentListViewModel
                {
                    Id = s.StudentID,
                    FullName = s.FullName,
                    NationalId = s.NationalID,
                    IsActiveForLearning = false
                })
                .OrderBy(s => s.FullName)
                .ToList();

            return View(students);
        }

        // =====================================================
        // ➕ إضافة طالب
        // =====================================================
        [HttpGet]
        public IActionResult Create()
        {
            if (!_subscriptionService.CanAddStudents(ActivePartnerId))
            {
                TempData["Error"] = "تم الوصول إلى الحد الأقصى للطلاب حسب الفترة الحالية.";
                return RedirectToAction(nameof(Index));
            }

            // 🟢 الدورات المتعاقد عليها في العقد النشط
            var courses = AllowedCourses
        .Select(c => new SelectListItem
        {
            Value = c.CourseId.ToString(),
            Text = c.CourseName
        })
        .OrderBy(x => x.Text)
        .ToList();


            var model = new CreatePartnerStudentViewModel
            {
                Courses = courses,
                Batches = new List<SelectListItem>()
            };

            return View(model);
        }


        // =====================================================
        // 🔁 جلب الدفعات حسب الفرع
        // =====================================================
        [HttpGet]
        public IActionResult GetBatchesByCourse(int courseId)
        {
            // 🔒 تحقق أن الدورة ضمن العقد
            var allowed = AllowedCourses.Any(c => c.CourseId == courseId);
            if (!allowed)
                return Json(new List<object>());

            // ✅ جلب دفعات الشريك فقط
            var batches = _context.Batches
                .Where(b =>
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == ActivePartnerId
                )
                .Select(b => new
                {
                    id = b.Id,
                    name = b.Name
                })
                .OrderBy(b => b.name)
                .ToList();

            return Json(batches);
        }

        // =====================================================
        // ➕ إضافة طالب (POST)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePartnerStudentViewModel model)
        {
            if (!_subscriptionService.CanAddStudents(ActivePartnerId))
            {
                ModelState.AddModelError("", "تم الوصول إلى الحد الأقصى للطلاب.");
                return View(model);
            }

            // 🔒 تحقق أن الدفعة تابعة لدورة متعاقد عليها
            var batch = _context.Batches
                .FirstOrDefault(b =>
                    b.Id == model.BatchId &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
            {
                ModelState.AddModelError("", "الدفعة غير صحيحة.");
                return View(model);
            }

            var isAllowedCourse = AllowedCourses
       .Any(c => c.CourseId == batch.CourseId);

            if (!isAllowedCourse)
            {
                ModelState.AddModelError("", "الدورة غير متاحة ضمن العقد.");
                return View(model);
            }


            var partner = _context.Partners.Find(ActivePartnerId);
            if (partner == null)
                return NotFound();

            string email = $"{model.NationalId}@{partner.Code}.edu.sa";

            var user = new ApplicationUser
            {
                UserName = model.NationalId,
                Email = email,
                PhoneNumber = model.Phone,
                FullName = model.FullName // ✅ الإصلاح الأساسي

            };

            var result = await _userManager.CreateAsync(user, model.NationalId);
            if (!result.Succeeded)
            {
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }

                    // إعادة تحميل القوائم
                    model.Courses = AllowedCourses
                        .Select(c => new SelectListItem
                        {
                            Value = c.CourseId.ToString(),
                            Text = c.CourseName
                        })
                        .OrderBy(x => x.Text)
                        .ToList();

                    model.Batches = new List<SelectListItem>();

                    return View(model);
                }

                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Student");

            var student = new Student
            {
                UserId = user.Id,
                FullName = model.FullName,
                NationalID = model.NationalId,
                BranchId = batch.BranchId,
                Gender = model.Gender, // ✅ مهم جدًا
                Level = model.Level, // ✅ الإضافة المطلوبة
                School = partner.Name,          // اسم المدرسة = اسم الشريك

                IsActiveForLearning = true,
            };

            _context.Students.Add(student);

            _context.StudentBatchEnrollments.Add(new StudentBatchEnrollment
            {
                Student = student,
                BatchId = model.BatchId,
                Status = "Active"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إضافة الطالب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // ✏️ تعديل الطالب
        // =====================================================
        public IActionResult Edit(int id)
        {
            var student = _context.Students
                    .Include(s => s.User)
                    .FirstOrDefault(s =>
                        s.StudentID == id &&
                        s.Branch.PartnerId == ActivePartnerId);


            if (student == null)
                return NotFound();

            var model = new EditPartnerStudentViewModel
            {
                Id = student.StudentID,
                FullName = student.FullName,
                Phone = student.User?.PhoneNumber,
                Gender = student.Gender,
                Level = student.Level,
                School = student.School,
                IsActiveForLearning = student.IsActiveForLearning,
            };

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditPartnerStudentViewModel model)
        {
            var student = _context.Students
                .Include(s => s.User)
                .FirstOrDefault(s =>
                    s.StudentID == model.Id &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            student.FullName = model.FullName;
            student.Gender = model.Gender;
            student.Level = model.Level;
            student.School = model.School;
            student.IsActiveForLearning = model.IsActiveForLearning;
            student.User.PhoneNumber = model.Phone;

            // 🖼️ رفع الصورة
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploads = Path.Combine("wwwroot/uploads/students");
                Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.ImageFile.FileName)}";
                var filePath = Path.Combine(uploads, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageFile.CopyToAsync(stream);

            }

            _context.SaveChanges();

            TempData["Success"] = "تم تحديث بيانات الطالب بنجاح.";
            return RedirectToAction(nameof(Index));
        }


        // =====================================================
        // 📄 تفاصيل الطالب (نسخة اختبار نظيفة)
        // =====================================================
        [HttpGet]
        public IActionResult Details(int id)
        {
            if (id <= 0)
                return NotFound();

            var student = _context.Students
                .AsNoTracking()
                .FirstOrDefault(s =>
                    s.StudentID == id &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            // 🟢 جلب دفعات الطالب (الخاصة بالشريك فقط)
            var batches = _context.StudentBatchEnrollments
                .Where(e =>
                    e.StudentID == id &&
                    e.Batch.Branch.PartnerId == ActivePartnerId)
                .Select(e => new PartnerStudentBatchVM
                {
                    BatchId = e.BatchId,
                    BatchName = e.Batch.Name,
                    CourseName = e.Batch.Course.Name,
                    EnrolledAt = e.EnrolledAt,
                    Status = e.Status
                })
                .OrderByDescending(b => b.EnrolledAt)
                .ToList();

            var model = new PartnerStudentDetailsViewModel
            {
                StudentId = student.StudentID,
                FullName = student.FullName,
                NationalId = student.NationalID,
                IsActiveForLearning = student.IsActiveForLearning,
                Batches = batches
            };

            return View(model);
        }


        // =====================================================
        // 🔐 إعادة تعيين كلمة المرور
        // =====================================================
        public IActionResult ResetPassword(int id)
        {
            var student = _context.Students
                .FirstOrDefault(s =>
                    s.StudentID == id &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            return View(new ResetStudentPasswordViewModel
            {
                StudentId = student.StudentID,
                NationalId = student.NationalID
            });
        }

        public IActionResult ArchiveStudent(int id)
        {
            var student = _context.Students.Find(id);
            if (student == null) return NotFound();

            student.IsActiveForLearning = false;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }


        public IActionResult ActivateStudent(int id)
        {
            var student = _context.Students.Find(id);
            if (student == null) return NotFound();

            student.IsActiveForLearning = true;
            _context.SaveChanges();

            return RedirectToAction("Archive");
        }

        // =====================================================
        // 📊 تقرير الطالب (Partner)
        // =====================================================
        [HttpGet]
        public IActionResult Report(int id)
        {
            if (id <= 0)
                return NotFound();

            // ===============================
            // 1️⃣ جلب الطالب (مقيد بالشريك)
            // ===============================
            var student = _context.Students
                .AsNoTracking()
                .FirstOrDefault(s =>
                                s.StudentID == id &&
                                s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            // ===============================
            // 2️⃣ جلب دفعات الطالب
            // ===============================
            var batches = _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e =>
                    e.StudentID == id &&
                    e.Batch.Branch.PartnerId == ActivePartnerId)
                .Select(e => new
                {
                    BatchName = e.Batch.Name,
                    CourseName = e.Batch.Course.Name
                })
                .ToList();

            // ===============================
            // 3️⃣ ViewModel خفيف
            // ===============================
            var model = new PartnerStudentReportViewModel
            {
                StudentId = student.StudentID,
                FullName = student.FullName,
                NationalId = student.NationalID,
                Phone = student.PhoneNumber,
                Level = student.Level,
                School = student.School,
                Batches = batches
                    .Select(b => $"{b.CourseName} - {b.BatchName}")
                    .ToList()
            };

            return View(model);
        }


        [HttpPost]
        public IActionResult BulkAddToBatch(int batchId, List<int> studentIds)
        {
            if (batchId <= 0 || studentIds == null || !studentIds.Any())
                return BadRequest();

            // ===============================
            // 1️⃣ تحقق من الدفعة
            // ===============================
            var batch = _context.Batches
                .Include(b => b.Branch)
                .FirstOrDefault(b =>
                    b.Id == batchId &&
                    b.Branch.PartnerId == ActivePartnerId);

            if (batch == null)
                return NotFound();

            // ===============================
            // 2️⃣ جلب طلاب الشريك فقط (بدون Contains)
            // ===============================
            var allPartnerStudents = _context.Students
                .Where(s => s.Branch.PartnerId == ActivePartnerId)
                .Select(s => s.StudentID)
                .ToList();

            // ===============================
            // 3️⃣ فلترة في الذاكرة
            // ===============================
            var studentIdsSet = new HashSet<int>(studentIds);

            var validStudentIds = allPartnerStudents
                .Where(id => studentIdsSet.Contains(id))
                .ToList();

            if (!validStudentIds.Any())
                return BadRequest();

            // ===============================
            // 4️⃣ منع التكرار
            // ===============================
            var existing = _context.StudentBatchEnrollments
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToList();

            var existingSet = new HashSet<int>(existing);

            // ===============================
            // 5️⃣ تجهيز الإدخال الجماعي
            // ===============================
            var newEnrollments = validStudentIds
                .Where(id => !existingSet.Contains(id))
                .Select(id => new StudentBatchEnrollment
                {
                    StudentID = id,
                    BatchId = batchId,
                    Status = "Active"
                })
                .ToList();

            if (!newEnrollments.Any())
                return RedirectToAction("Details", "Batches", new { id = batchId });

            // ===============================
            // 6️⃣ Bulk Insert
            // ===============================
            _context.BulkInsert(newEnrollments);

            return RedirectToAction("Details", "Batches", new { id = batchId });
        }


        [HttpGet]
        public IActionResult GetAvailableStudents(int courseId)
        {
            var students = _context.Students
                .Where(s =>
                    s.Branch.PartnerId == ActivePartnerId &&
                    s.IsActiveForLearning)
                .Select(s => new
                {
                    id = s.StudentID,
                    name = s.FullName,
                    nationalId = s.NationalID
                })
                .OrderBy(s => s.name)
                .ToList();

            return Json(students);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetStudentPasswordViewModel model)
        {
            var student = _context.Students
                .FirstOrDefault(s =>
                    s.StudentID == model.StudentId &&
                    s.Branch.PartnerId == ActivePartnerId);

            if (student == null)
                return NotFound();

            var user = await _userManager.FindByIdAsync(student.UserId);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            await _userManager.ResetPasswordAsync(user, token, student.NationalID);

            TempData["Success"] = "تم إعادة تعيين كلمة المرور إلى رقم الهوية.";
            return RedirectToAction(nameof(Index));
        }
    }
}
