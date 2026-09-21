using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;
using System.Linq;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstructorCurriculumBatchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILectureInstructorSyncService _lectureSyncService;

        public InstructorCurriculumBatchesController(ApplicationDbContext context, ILectureInstructorSyncService lectureSyncService)
        {
            _context = context;
            _lectureSyncService = lectureSyncService;
        }

        // ✅ عرض جميع الربط Instructor-Curriculum-Batch
        public IActionResult Index()
        {
            var data = _context.InstructorCurriculumBatches
        .Include(x => x.Instructor)
        .Include(x => x.Curriculum)
        .Include(x => x.Batch)
        .Select(x => new InstructorCurriculumBatchViewModel
        {
            Id = x.Id,
            InstructorName = x.Instructor.FullName,
            CurriculumName = x.Curriculum.Title,
            BatchName = x.Batch.Name,
            BatchGenderName = x.Batch.Gender.ToString() // 👈 هنا نحول جنس الدفعة إلى نص
        })
        .ToList();


            return View(data);
        }

        // ✅ عرض صفحة إضافة ربط جديد
        // ✅ عرض صفحة إضافة ربط جديد
        public IActionResult Create()
        {
            var model = new InstructorCurriculumBatchViewModel
            {
                Curriculums = _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToList(),

                Batches = _context.Batches
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToList(),

                Instructors = new List<SelectListItem>() // سيتم تعبئته ديناميكيًا لاحقًا حسب اختيار الدفعة
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstructorCurriculumBatchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns(model);
                return View(model);
            }

            var batch = _context.Batches.Find(model.BatchId);
            if (batch == null)
            {
                ModelState.AddModelError("", "الدفعة المختارة غير موجودة.");
                LoadDropdowns(model);
                return View(model);
            }

            var instructor = _context.Instructors.Find(model.InstructorId);
            if (instructor == null)
            {
                ModelState.AddModelError("", "المدرب المختار غير موجود.");
                LoadDropdowns(model);
                return View(model);
            }

            // ✅ تحقق من توافق الجنس
     

            // ✅ منع التكرار يدويًا
            var exists = _context.InstructorCurriculumBatches.Any(x =>
                x.InstructorId == model.InstructorId &&
                x.CurriculumId == model.CurriculumId &&
                x.BatchId == model.BatchId);

            if (exists)
            {
                ModelState.AddModelError("", "⚠️ هذا الربط موجود مسبقًا ولا يمكن تكراره.");
                LoadDropdowns(model);
                return View(model);
            }

            var newRelation = new InstructorCurriculumBatch
            {
                InstructorId = model.InstructorId,
                CurriculumId = model.CurriculumId,
                BatchId = model.BatchId,
                UserId = instructor.UserId // ✅ إضافة الـ UserId إذا كان موجودًا
            };

            try
            {
                _context.InstructorCurriculumBatches.Add(newRelation);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ خطأ أثناء الحفظ: " + (ex.InnerException?.Message ?? ex.Message));
                LoadDropdowns(model);
                return View(model);
            }

            await _lectureSyncService.SyncAutoLecturesAsync(newRelation.CurriculumId, newRelation.BatchId, newRelation.InstructorId);

            TempData["SuccessMessage"] = "✅ تم ربط المدرب بالدفعة والمنهج بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/InstructorCurriculumBatches/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.InstructorCurriculumBatches
                .Include(x => x.Batch)
                .Include(x => x.Curriculum)
                .Include(x => x.Instructor)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
                return NotFound();

            var model = new InstructorCurriculumBatchViewModel
            {
                Id = item.Id,
                BatchId = item.BatchId,
                CurriculumId = item.CurriculumId,
                InstructorId = item.InstructorId,
                BatchName = item.Batch?.Name,
                BatchGenderName = item.Batch?.Gender.ToString(),

                Batches = await _context.Batches
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync(),

                Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync(),

                Instructors = await _context.Instructors
                    .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.FullName })
                    .ToListAsync()
            };

            return View(model);
        }

        // POST: Admin/InstructorCurriculumBatches/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InstructorCurriculumBatchViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.Batches = await _context.Batches
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync();

                model.Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync();

                model.Instructors = await _context.Instructors
                    .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.FullName })
                    .ToListAsync();

                return View(model);
            }

            var batch = await _context.Batches.FindAsync(model.BatchId);
            var instructor = await _context.Instructors.FindAsync(model.InstructorId);

            if (batch == null || instructor == null)
            {
                ModelState.AddModelError("", "الدفعة أو المدرب غير موجود.");
                LoadDropdowns(model);
                return View(model);
            }

            // ✅ تحقق من توافق الجنس
     

            // ✅ تحقق من التكرار مع استثناء السطر الحالي
            var duplicateExists = await _context.InstructorCurriculumBatches.AnyAsync(x =>
                x.Id != id &&
                x.InstructorId == model.InstructorId &&
                x.CurriculumId == model.CurriculumId &&
                x.BatchId == model.BatchId);

            if (duplicateExists)
            {
                ModelState.AddModelError("", "⚠️ يوجد ربط مماثل بالفعل ولا يمكن تكراره.");
                LoadDropdowns(model);
                return View(model);
            }

            var entity = await _context.InstructorCurriculumBatches.FindAsync(id);
            if (entity == null)
                return NotFound();

            entity.InstructorId = model.InstructorId;
            entity.CurriculumId = model.CurriculumId;
            entity.BatchId = model.BatchId;
            entity.UserId = instructor.UserId; // ✅ لتفادي مشاكل الحفظ

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "❌ خطأ أثناء الحفظ: " + (ex.InnerException?.Message ?? ex.Message));
                LoadDropdowns(model);
                return View(model);
            }

            await _lectureSyncService.SyncAutoLecturesAsync(entity.CurriculumId, entity.BatchId, entity.InstructorId);

            TempData["SuccessMessage"] = "✅ تم تعديل الربط بنجاح!";
            return RedirectToAction(nameof(Index));
        }


        // ✅ AJAX: المناهج التي هذا المدرب مؤهل لها بالفعل (من CurriculumInstructors)
        [HttpGet]
        public async Task<JsonResult> GetCurriculumsForInstructor(int instructorId)
        {
            var curriculums = await _context.CurriculumInstructors
                .Where(ci => ci.InstructorId == instructorId)
                .Select(ci => new { id = ci.CurriculumId, name = ci.Curriculum.Title })
                .Distinct()
                .ToListAsync();

            return Json(new { success = true, data = curriculums });
        }

        // ✅ AJAX: الدفعات المتاحة لربط هذا المدرب بها ضمن منهج معيّن (تستبعد الدفعات المربوطة مسبقًا وتفلتر حسب جنس المدرب)
        [HttpGet]
        public async Task<JsonResult> GetBatchesForInstructor(int instructorId, int curriculumId)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null)
                return Json(new { success = false, message = "المدرب غير موجود." });

            var linkedBatchIds = await _context.InstructorCurriculumBatches
                .Where(x => x.InstructorId == instructorId && x.CurriculumId == curriculumId)
                .Select(x => x.BatchId)
                .ToListAsync();

            var batchesQuery = _context.Batches.Where(b => !linkedBatchIds.Contains(b.Id));

            if (instructor.Gender == GenderType.Male)
                batchesQuery = batchesQuery.Where(b => b.Gender == BatchGender.ذكور || b.Gender == BatchGender.مختلط);
            else if (instructor.Gender == GenderType.Female)
                batchesQuery = batchesQuery.Where(b => b.Gender == BatchGender.إناث || b.Gender == BatchGender.مختلط);

            var batches = await batchesQuery
                .Select(b => new { id = b.Id, name = b.Name })
                .ToListAsync();

            return Json(new { success = true, data = batches });
        }

        // ✅ AJAX: حفظ ربط مدرب بدفعة مباشرة من صفحة Instructors/Index (بدون تنقل)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax(int instructorId, int curriculumId, int batchId)
        {
            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null)
                return Json(new { success = false, message = "الدفعة غير موجودة." });

            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null)
                return Json(new { success = false, message = "المدرب غير موجود." });

            bool exists = await _context.InstructorCurriculumBatches.AnyAsync(x =>
                x.InstructorId == instructorId && x.CurriculumId == curriculumId && x.BatchId == batchId);

            if (exists)
                return Json(new { success = false, message = "⚠️ هذا الربط موجود مسبقًا." });

            var entity = new InstructorCurriculumBatch
            {
                InstructorId = instructorId,
                CurriculumId = curriculumId,
                BatchId = batchId,
                UserId = instructor.UserId
            };

            _context.InstructorCurriculumBatches.Add(entity);
            await _context.SaveChangesAsync();

            var syncedCount = await _lectureSyncService.SyncAutoLecturesAsync(curriculumId, batchId, instructorId);

            return Json(new { success = true, message = "تم ربط المدرب بالدفعة بنجاح.", batchName = batch.Name, syncedLecturesCount = syncedCount });
        }

        [HttpGet]
        public JsonResult GetInstructorsByBatchAndCurriculum(int batchId, int curriculumId)
        {
            var batch = _context.Batches.Find(batchId);
            if (batch == null)
                return Json(new { success = false, message = "الدفعة غير موجودة" });

            var curriculumInstructors = _context.CurriculumInstructors
                .Include(ci => ci.Instructor)
                .Where(ci => ci.CurriculumId == curriculumId)
                .AsQueryable();

            if (batch.Gender == BatchGender.ذكور)
            {
                curriculumInstructors = curriculumInstructors.Where(ci => ci.Instructor.Gender == GenderType.Male);
            }
            else if (batch.Gender == BatchGender.إناث)
            {
                curriculumInstructors = curriculumInstructors.Where(ci => ci.Instructor.Gender == GenderType.Female);
            }
            // لو مختلط ➔ لا نفلتر بالجنس

            var instructors = curriculumInstructors
                .Select(ci => new
                {
                    id = ci.Instructor.Id,
                    name = ci.Instructor.FullName
                })
                .Distinct()
                .ToList();

            return Json(new
            {
                success = true,
                data = instructors,
                batchGender = GetGenderDisplayName(batch.Gender)
            });
        }

        private string GetGenderDisplayName(BatchGender gender)
        {
            switch (gender)
            {
                case BatchGender.ذكور:
                    return "ذكور";
                case BatchGender.إناث:
                    return "إناث";
                case BatchGender.مختلط:
                    return "مختلط";
                default:
                    return "غير معروف";
            }
        }


        // ✅ حذف الربط + إزاحة المحاضرات التلقائية لمدرب بديل إن وُجد بنفس المنهج والدفعة
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.InstructorCurriculumBatches.FindAsync(id);
            if (item == null)
                return NotFound();

            var curriculumId = item.CurriculumId;
            var batchId = item.BatchId;

            _context.InstructorCurriculumBatches.Remove(item);
            await _context.SaveChangesAsync();

            var replacementInstructorId = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x => x.CurriculumId == curriculumId && x.BatchId == batchId)
                .Select(x => x.InstructorId)
                .FirstOrDefaultAsync();

            if (replacementInstructorId != 0)
            {
                await _lectureSyncService.SyncAutoLecturesAsync(curriculumId, batchId, replacementInstructorId);
                TempData["SuccessMessage"] = "🗑️ تم حذف الربط بنجاح، وتم إزاحة محاضراته التلقائية للمدرب البديل المرتبط بنفس المنهج والدفعة.";
            }
            else
            {
                TempData["WarningMessage"] = "🗑️ تم حذف الربط بنجاح، لكن لا يوجد مدرب بديل مرتبط بنفس المنهج والدفعة — محاضرات هذه الدفعة ستبقى منسوبة للمدرب السابق حتى تُعيد ربطها يدويًا أو تختار مدربًا آخر.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ✅ AJAX: ملخص حالة تزامن المحاضرات لكل الربط النشط — يُستخدم في شاشة الربط لعرض عمود "حالة المحاضرات"
        // ⚠️ Known limitation: يدور على كل الروابط النشطة (N+1 منطقي) — مقبول لحجم البيانات الحالي، حوّله لاستعلام SQL مجمّع واحد إن تجاوز عدد الروابط بضع مئات
        [HttpGet]
        public async Task<JsonResult> GetOutOfSyncSummary()
        {
            var activeLinks = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Select(x => new { x.Id, x.CurriculumId, x.BatchId, x.InstructorId, InstructorName = x.Instructor.FullName, BatchName = x.Batch.Name })
                .ToListAsync();

            var result = new List<object>();
            foreach (var link in activeLinks)
            {
                var status = await _lectureSyncService.GetSyncStatusAsync(link.CurriculumId, link.BatchId, link.InstructorId);
                result.Add(new
                {
                    id = link.Id,
                    batchName = link.BatchName,
                    instructorName = link.InstructorName,
                    outOfSyncCount = status.OutOfSyncCount,
                    totalAutoLectures = status.TotalAutoLectures,
                    isFullySynced = status.IsFullySynced
                });
            }

            return Json(new { success = true, data = result });
        }

        // ✅ AJAX: إعادة مزامنة يدوية لمحاضرات ربط واحد — تُستخدم من زر "إعادة مزامنة الآن" في شاشة الربط
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ResyncLectures(int id)
        {
            var link = await _context.InstructorCurriculumBatches.FindAsync(id);
            if (link == null)
                return Json(new { success = false, message = "الربط غير موجود." });

            var syncedCount = await _lectureSyncService.SyncAutoLecturesAsync(link.CurriculumId, link.BatchId, link.InstructorId);

            return Json(new { success = true, message = $"تمت مزامنة {syncedCount} محاضرة.", syncedCount });
        }

        private void LoadDropdowns(InstructorCurriculumBatchViewModel model)
        {
            model.Curriculums = _context.Curriculums
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToList();

            model.Batches = _context.Batches
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToList();

            model.Instructors = _context.Instructors
                .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.FullName })
                .ToList();
        }
    }
}
