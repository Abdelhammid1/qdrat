using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.StudentBatchEnrollments;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudentBatchEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentBatchEnrollmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Index
        public async Task<IActionResult> Index()
        {
            var model = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .OrderByDescending(e => e.EnrolledAt)
                .Select(e => new StudentBatchEnrollmentListViewModel
                {
                    Id = e.Id,
                    StudentName = e.Student.FullName,
                    BatchName = e.Batch.Name,
                    EnrolledAt = e.EnrolledAt,
                    Status = e.Status
                })
                .ToListAsync();

            return View(model);
        }


        public async Task<IActionResult> Details(int id)
        {
            var currentEnrollment = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new
                {
                    e.Id,
                    e.StudentID,
                    e.BatchId,
                    StudentName = e.Student.FullName,
                    BatchName = e.Batch.Name,
                    e.EnrolledAt,
                    e.Status
                })
                .FirstOrDefaultAsync();

            if (currentEnrollment == null)
                return NotFound();

            var relatedBatches = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == currentEnrollment.StudentID)
                .OrderByDescending(e => e.EnrolledAt)
                .Select(e => new StudentBatchEnrollmentRelatedBatchVm
                {
                    EnrollmentId = e.Id,
                    StudentID = e.StudentID,
                    BatchId = e.BatchId,
                    StudentName = e.Student.FullName,
                    BatchName = e.Batch.Name,
                    EnrolledAt = e.EnrolledAt,
                    Status = e.Status,
                    IsCurrent = e.Id == id
                })
                .ToListAsync();

            var model = new StudentBatchEnrollmentDetailsViewModel
            {
                Id = currentEnrollment.Id,
                StudentID = currentEnrollment.StudentID,
                BatchId = currentEnrollment.BatchId,
                StudentName = currentEnrollment.StudentName,
                BatchName = currentEnrollment.BatchName,
                EnrolledAt = currentEnrollment.EnrolledAt,
                Status = currentEnrollment.Status,
                RelatedBatches = relatedBatches
            };

            return View(model);
        }




        // GET: Create
        [HttpGet]
        public IActionResult Create(int? studentId = null)
        {
            var vm = new StudentBatchEnrollmentFormViewModel
            {
                StudentID = studentId ?? 0,
                Students = _context.Students
                    .Select(s => new SelectListItem { Value = s.StudentID.ToString(), Text = s.FullName })
                    .ToList(),

                Batches = _context.Batches
                    .OrderByDescending(b => b.Id)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentBatchEnrollmentFormViewModel vm)
        {
            if (!ModelState.IsValid || vm.SelectedBatchIds.Count == 0)
            {
                if (vm.SelectedBatchIds.Count == 0)
                    ModelState.AddModelError("", "يجب اختيار دفعة واحدة على الأقل");

                vm.Students = _context.Students
                    .Select(s => new SelectListItem { Value = s.StudentID.ToString(), Text = s.FullName })
                    .ToList();

                vm.Batches = _context.Batches
                    .OrderByDescending(b => b.Id)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToList();

                return View(vm);
            }

            foreach (var batchId in vm.SelectedBatchIds)
            {
                bool exists = await (from e in _context.StudentBatchEnrollments
                                     where e.StudentID == vm.StudentID && e.BatchId == batchId
                                     select e.Id).AnyAsync();

                if (!exists)
                {
                    var enrollment = new StudentBatchEnrollment
                    {
                        StudentID = vm.StudentID,
                        BatchId = batchId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = "Active"
                    };
                    _context.StudentBatchEnrollments.Add(enrollment);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم ربط الطالب بالدفعات المحددة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var studentEnrollment = await _context.StudentBatchEnrollments
                .FirstOrDefaultAsync(e => e.Id == id);

            if (studentEnrollment == null) return NotFound();

            var studentId = studentEnrollment.StudentID;

           
            var selectedBatchIds = await (from e in _context.StudentBatchEnrollments
                                          where e.StudentID == studentId
                                          select e.BatchId).ToListAsync();

            var vm = new StudentBatchEnrollmentFormViewModel
            {
                StudentID = studentId,
                SelectedBatchIds = selectedBatchIds,

                Students = await _context.Students
                    .Select(s => new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName,
                        Selected = s.StudentID == studentId
                    })
                    .ToListAsync(),

                Batches = (await _context.Batches
                    .OrderByDescending(b => b.Id)
                    .Select(b => new { b.Id, b.Name })
                    .ToListAsync()) // ننفذ SQL هنا ونرجع للذاكرة
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name,
                        Selected = selectedBatchIds.Contains(b.Id) // الفلترة على الذاكرة
                    })
                    .ToList()
            };


            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StudentBatchEnrollmentFormViewModel vm)
        {
            if (!ModelState.IsValid || vm.SelectedBatchIds.Count == 0)
            {
                if (vm.SelectedBatchIds.Count == 0)
                    ModelState.AddModelError("", "يجب اختيار دفعة واحدة على الأقل");

                vm.Students = _context.Students
                    .Select(s => new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName,
                        Selected = s.StudentID == vm.StudentID
                    })
                    .ToList();

                vm.Batches = _context.Batches
                    .OrderByDescending(b => b.Id)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name,
                        Selected = vm.SelectedBatchIds.Any(sb => sb == b.Id)
                    })
                    .ToList();

                return View(vm);
            }

            // حذف الدفعات القديمة لهذا الطالب
            var oldEnrollments = _context.StudentBatchEnrollments
                .Where(e => e.StudentID == vm.StudentID)
                .ToList();
            _context.StudentBatchEnrollments.RemoveRange(oldEnrollments);

            // إضافة الدفعات الجديدة
            foreach (var batchId in vm.SelectedBatchIds)
            {
                var enrollment = new StudentBatchEnrollment
                {
                    StudentID = vm.StudentID,
                    BatchId = batchId,
                    EnrolledAt = DateTime.UtcNow,
                    Status = "Active"
                };
                _context.StudentBatchEnrollments.Add(enrollment);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تحديث دفعات الطالب بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // POST: Delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.StudentBatchEnrollments.FindAsync(id);
            if (entity != null)
            {
                _context.StudentBatchEnrollments.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "❌ تم حذف الربط";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
