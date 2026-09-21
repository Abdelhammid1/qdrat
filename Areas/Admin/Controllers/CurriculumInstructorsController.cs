using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Curriculum;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CurriculumInstructorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CurriculumInstructorsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var items = await _context.CurriculumInstructors
                .Include(ci => ci.Curriculum)
                .Include(ci => ci.Instructor)
                .ToListAsync();

            var list = items.Select(ci => new CurriculumInstructorViewModel
            {
                Id = ci.Id, // ✅ ضروري جدًا
                CurriculumId = ci.CurriculumId,
                InstructorId = ci.InstructorId,
                Gender = ci.Gender,
                AssignedDate = ci.AssignedDate,
                Notes = ci.Notes,

                CurriculumTitle = ci.Curriculum.Title,       // ✅ بديل مباشر عن استخدام SelectList
                InstructorName = ci.Instructor.FullName,     // ✅ بديل مباشر عن استخدام SelectList

                CurriculumList = new List<SelectListItem>
        {
            new SelectListItem { Value = ci.CurriculumId.ToString(), Text = ci.Curriculum.Title }
        },

                InstructorList = new List<SelectListItem>
        {
            new SelectListItem { Value = ci.InstructorId.ToString(), Text = ci.Instructor.FullName }
        }
            }).ToList();

            return View(list);
        }


        // GET: Admin/CurriculumInstructors/Create
        public async Task<IActionResult> Create(int? instructorId = null, bool returnToInstructors = false)
        {
            var vm = new CurriculumInstructorViewModel
            {
                CurriculumList = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync(),

                InstructorList = await _context.Instructors
                    .Select(i => new SelectListItem
                    {
                        Value = i.Id.ToString(),
                        Text = i.FullName,
                        Selected = instructorId.HasValue && i.Id == instructorId.Value
                    })
                    .ToListAsync(),

                AssignedDate = DateTime.Now,
                ReturnToInstructors = returnToInstructors // نحتفظ بالاختيار داخل الفيوموديل
            };

            return View(vm);
        }



        // POST: Admin/CurriculumInstructors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CurriculumInstructorViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var entity = new CurriculumInstructor
                {
                    CurriculumId = vm.CurriculumId,
                    InstructorId = vm.InstructorId,
                    Gender = vm.Gender,
                    AssignedDate = vm.AssignedDate,
                    Notes = vm.Notes
                };

                _context.CurriculumInstructors.Add(entity);
                await _context.SaveChangesAsync();

                if (vm.ReturnToInstructors)
                {
                    // ✅ إعادة التوجيه لصفحة المدربين
                    return RedirectToAction("Index", "Instructors", new { area = "Admin" });
                }

                return RedirectToAction(nameof(Index));
            }

            // في حال وجود خطأ في الإدخال نعيد تعبئة القوائم
            vm.CurriculumList = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToListAsync();

            vm.InstructorList = await _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName,
                    Selected = i.Id == vm.InstructorId
                }).ToListAsync();

            return View(vm);
        }

        // ✅ AJAX: ربط مدرب بمنهج مباشرة من صفحة Instructors/Index (بدون تنقل)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax(int instructorId, int curriculumId, string? notes)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null)
                return Json(new { success = false, message = "المدرب غير موجود." });

            var curriculum = await _context.Curriculums.FindAsync(curriculumId);
            if (curriculum == null)
                return Json(new { success = false, message = "المنهج غير موجود." });

            bool exists = await _context.CurriculumInstructors.AnyAsync(x =>
                x.InstructorId == instructorId && x.CurriculumId == curriculumId);

            if (exists)
                return Json(new { success = false, message = "هذا المدرب مؤهل بالفعل لهذا المنهج." });

            var entity = new CurriculumInstructor
            {
                InstructorId = instructorId,
                CurriculumId = curriculumId,
                Gender = instructor.Gender,
                AssignedDate = DateTime.Now,
                Notes = notes
            };

            _context.CurriculumInstructors.Add(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم تأهيل المدرب لهذا المنهج بنجاح.", curriculumTitle = curriculum.Title });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var curriculumInstructor = await _context.CurriculumInstructors
                .Include(c => c.Curriculum)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (curriculumInstructor == null)
                return NotFound();

            var viewModel = new CurriculumInstructorViewModel
            {
                Id = curriculumInstructor.Id,
                CurriculumId = curriculumInstructor.CurriculumId,
                InstructorId = curriculumInstructor.InstructorId,
                Gender = curriculumInstructor.Gender,
                AssignedDate = curriculumInstructor.AssignedDate,
                Notes = curriculumInstructor.Notes,
                CurriculumTitle = curriculumInstructor.Curriculum?.Title ?? "❌ غير موجود",
                InstructorName = curriculumInstructor.Instructor?.FullName ?? "❌ غير موجود"
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var entity = await _context.CurriculumInstructors
                .Include(c => c.Curriculum)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (entity == null)
                return NotFound();

            var vm = new CurriculumInstructorViewModel
            {
                Id = entity.Id,
                CurriculumId = entity.CurriculumId,
                InstructorId = entity.InstructorId,
                Gender = entity.Gender,
                AssignedDate = entity.AssignedDate,
                Notes = entity.Notes,
                ReturnToInstructors = false
            };

            // ✅ تعبئة القوائم المنسدلة مع تحديد العنصر المختار
            vm.CurriculumList = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = c.Id == entity.CurriculumId
                }).ToListAsync();

            vm.InstructorList = await _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName,
                    Selected = i.Id == entity.InstructorId
                }).ToListAsync();

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CurriculumInstructorViewModel vm)
        {
            if (id != vm.Id)
                return BadRequest(); // تطابق الأرقام لضمان سلامة التعديل

            var item = await _context.CurriculumInstructors.FindAsync(id);
            if (item == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                item.CurriculumId = vm.CurriculumId;
                item.InstructorId = vm.InstructorId;
                item.Gender = vm.Gender;
                item.AssignedDate = vm.AssignedDate;
                item.Notes = vm.Notes;

                await _context.SaveChangesAsync();

                // شرط التحويل حسب الزر المختار مسبقًا
                if (vm.ReturnToInstructors)
                    return RedirectToAction("Index", "Instructors", new { area = "Admin" });

                return RedirectToAction(nameof(Index));
            }

            // عند وجود أخطاء، نعيد تعبئة القوائم المنسدلة مع تحديد العنصر المختار
            vm.CurriculumList = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = c.Id == vm.CurriculumId
                }).ToListAsync();

            vm.InstructorList = await _context.Instructors
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.FullName,
                    Selected = i.Id == vm.InstructorId
                }).ToListAsync();

            return View(vm);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var entity = await _context.CurriculumInstructors
                .Include(c => c.Curriculum)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (entity == null)
                return NotFound();

            var vm = new CurriculumInstructorViewModel
            {
                Id = entity.Id,
                CurriculumId = entity.CurriculumId,
                InstructorId = entity.InstructorId,
                Gender = entity.Gender,
                AssignedDate = entity.AssignedDate,
                Notes = entity.Notes,
                CurriculumTitle = entity.Curriculum?.Title ?? "❌ غير موجود",
                InstructorName = entity.Instructor?.FullName ?? "❌ غير موجود"
            };

            return View(vm);
        }



        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var entity = await _context.CurriculumInstructors.FindAsync(id);

            if (entity == null)
                return NotFound();

            _context.CurriculumInstructors.Remove(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // سيتم لاحقًا: Index, Edit, Details, Delete
    }
}
