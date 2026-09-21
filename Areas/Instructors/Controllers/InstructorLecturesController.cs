using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.Lectures;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorLecturesController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public InstructorLecturesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? batchId)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var batches = await GetInstructorBatchOptionsAsync(instructorId);

            var lecturesQuery =
                from lecture in _context.Lecture.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                join course in _context.Courses.AsNoTracking()
                    on lecture.CourseId equals course.Id
                join section in _context.Sections.AsNoTracking()
                    on lecture.SectionId equals section.Id
                where lecture.InstructorId == instructorId
                select new
                {
                    lecture.Id,
                    lecture.Title,
                    lecture.Location,
                    lecture.Date,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseName = course.Name,
                    SectionTitle = section.Title
                };

            if (batchId.HasValue && batchId.Value > 0)
            {
                var allowed = await HasAccessToBatchAsync(instructorId, batchId.Value);

                if (!allowed)
                {
                    return Forbid();
                }

                lecturesQuery = lecturesQuery.Where(x => x.BatchId == batchId.Value);
            }

            var lectures = await lecturesQuery
                .OrderByDescending(x => x.Date)
                .Select(x => new InstructorLectureListItemViewModel
                {
                    LectureId = x.Id,
                    Title = x.Title,
                    BatchName = x.BatchName,
                    CourseName = x.CourseName,
                    SectionTitle = x.SectionTitle,
                    Location = x.Location ?? "",
                    Date = x.Date
                })
                .ToListAsync();

            var model = new InstructorLecturesIndexViewModel
            {
                BatchId = batchId,
                Batches = batches,
                Lectures = lectures
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? batchId)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var model = new InstructorLectureFormViewModel
            {
                Date = DateTime.Now,
                BatchId = batchId ?? 0,
                Batches = await GetInstructorBatchOptionsAsync(instructorId)
            };

            if (batchId.HasValue && batchId.Value > 0)
            {
                var allowed = await HasAccessToBatchAsync(instructorId, batchId.Value);

                if (!allowed)
                {
                    return Forbid();
                }

                model.CourseId = await GetBatchCourseIdAsync(batchId.Value);
                model.Sections = await GetInstructorSectionOptionsAsync(instructorId, batchId.Value);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstructorLectureFormViewModel model)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var hasAccess = await HasAccessToBatchAsync(instructorId, model.BatchId);

            if (!hasAccess)
            {
                return Forbid();
            }

            var courseId = await GetBatchCourseIdAsync(model.BatchId);

            if (courseId <= 0)
            {
                ModelState.AddModelError(nameof(model.BatchId), "لم يتم العثور على الدورة المرتبطة بالدفعة.");
            }

            var sectionAllowed = await HasAccessToSectionInBatchAsync(instructorId, model.BatchId, model.SectionId);

            if (!sectionAllowed)
            {
                ModelState.AddModelError(nameof(model.SectionId), "المحور المحدد غير مرتبط بصلاحياتك داخل هذه الدفعة.");
            }

            if (!ModelState.IsValid)
            {
                model.CourseId = courseId;
                model.Batches = await GetInstructorBatchOptionsAsync(instructorId);
                model.Sections = model.BatchId > 0
                    ? await GetInstructorSectionOptionsAsync(instructorId, model.BatchId)
                    : new List<SelectListItem>();

                return View(model);
            }

            var lecture = new Lecture
            {
                Title = model.Title.Trim(),
                Location = string.IsNullOrWhiteSpace(model.Location) ? "" : model.Location.Trim(),
                Date = model.Date,
                InstructorId = instructorId,
                BatchId = model.BatchId,
                CourseId = courseId,
                SectionId = model.SectionId
            };

            await _context.Lecture.AddAsync(lecture);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إنشاء المحاضرة بنجاح.";
            return RedirectToAction(nameof(Index), new { batchId = model.BatchId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var lecture = await _context.Lecture
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.InstructorId == instructorId);

            if (lecture == null)
            {
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");
            }

            var hasAccess = await HasAccessToBatchAsync(instructorId, lecture.BatchId);

            if (!hasAccess)
            {
                return Forbid();
            }

            var model = new InstructorLectureFormViewModel
            {
                Id = lecture.Id,
                Title = lecture.Title,
                Location = lecture.Location,
                Date = lecture.Date,
                BatchId = lecture.BatchId,
                CourseId = lecture.CourseId,
                SectionId = lecture.SectionId,
                Batches = await GetInstructorBatchOptionsAsync(instructorId),
                Sections = await GetInstructorSectionOptionsAsync(instructorId, lecture.BatchId)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InstructorLectureFormViewModel model)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var lecture = await _context.Lecture
                .FirstOrDefaultAsync(x =>
                    x.Id == model.Id &&
                    x.InstructorId == instructorId);

            if (lecture == null)
            {
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");
            }

            var hasAccess = await HasAccessToBatchAsync(instructorId, model.BatchId);

            if (!hasAccess)
            {
                return Forbid();
            }

            var courseId = await GetBatchCourseIdAsync(model.BatchId);

            if (courseId <= 0)
            {
                ModelState.AddModelError(nameof(model.BatchId), "لم يتم العثور على الدورة المرتبطة بالدفعة.");
            }

            var sectionAllowed = await HasAccessToSectionInBatchAsync(instructorId, model.BatchId, model.SectionId);

            if (!sectionAllowed)
            {
                ModelState.AddModelError(nameof(model.SectionId), "المحور المحدد غير مرتبط بصلاحياتك داخل هذه الدفعة.");
            }

            if (!ModelState.IsValid)
            {
                model.CourseId = courseId;
                model.Batches = await GetInstructorBatchOptionsAsync(instructorId);
                model.Sections = model.BatchId > 0
                    ? await GetInstructorSectionOptionsAsync(instructorId, model.BatchId)
                    : new List<SelectListItem>();

                return View(model);
            }

            lecture.Title = model.Title.Trim();
            lecture.Location = string.IsNullOrWhiteSpace(model.Location) ? "" : model.Location.Trim();
            lecture.Date = model.Date;
            lecture.BatchId = model.BatchId;
            lecture.CourseId = courseId;
            lecture.SectionId = model.SectionId;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تعديل المحاضرة بنجاح.";
            return RedirectToAction(nameof(Index), new { batchId = model.BatchId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var lecture = await (
                from l in _context.Lecture.AsNoTracking()
                join b in _context.Batches.AsNoTracking()
                    on l.BatchId equals b.Id
                join c in _context.Courses.AsNoTracking()
                    on l.CourseId equals c.Id
                join s in _context.Sections.AsNoTracking()
                    on l.SectionId equals s.Id
                where l.Id == id && l.InstructorId == instructorId
                select new InstructorLectureListItemViewModel
                {
                    LectureId = l.Id,
                    Title = l.Title,
                    BatchName = b.Name,
                    CourseName = c.Name,
                    SectionTitle = s.Title,
                    Location = l.Location ?? "",
                    Date = l.Date
                }
            ).FirstOrDefaultAsync();

            if (lecture == null)
            {
                return NotFound("لم يتم العثور على المحاضرة أو لا تملك صلاحية عليها.");
            }

            return View(lecture);
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByBatch(int batchId)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Json(new List<object>());
            }

            var allowed = await HasAccessToBatchAsync(instructorId, batchId);

            if (!allowed)
            {
                return Json(new List<object>());
            }

            var sections = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join section in _context.Sections.AsNoTracking()
                    on link.CurriculumId equals section.CurriculumId
                where link.InstructorId == instructorId &&
                      link.BatchId == batchId
                orderby section.Title
                select new
                {
                    id = section.Id,
                    title = section.Title
                }
            ).ToListAsync();

            return Json(sections);
        }

        private async Task<List<SelectListItem>> GetInstructorBatchOptionsAsync(int instructorId)
        {
            var today = DateTime.Today;

            var directBatches = await (
                from role in _context.Set<InstructorBatchRole>().AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                where role.InstructorId == instructorId
                    && !batch.IsArchived
                    && (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select new InstructorBatchOptionRaw
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                }
            ).ToListAsync();

            var curriculumBatches = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on link.BatchId equals batch.Id
                where link.InstructorId == instructorId
                    && !batch.IsArchived
                    && (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select new InstructorBatchOptionRaw
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                }
            ).ToListAsync();

            var graduatedBatches = await (
                from access in _context.Set<BatchInstructorGraduatedAccess>().AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on access.BatchId equals batch.Id
                where access.InstructorId == instructorId
                    && access.IsActive
                    && !batch.IsArchived
                    && batch.EndDate.HasValue
                    && batch.EndDate.Value < today
                select new InstructorBatchOptionRaw
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                }
            ).ToListAsync();

            var batches = directBatches
                .Concat(curriculumBatches)
                .Concat(graduatedBatches)
                .GroupBy(x => x.BatchId)
                .Select(g => new SelectListItem
                {
                    Value = g.Key.ToString(),
                    Text = g.First().BatchName
                })
                .OrderBy(x => x.Text)
                .ToList();

            return batches;
        }

        private async Task<List<SelectListItem>> GetInstructorSectionOptionsAsync(int instructorId, int batchId)
        {
            var sections = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join section in _context.Sections.AsNoTracking()
                    on link.CurriculumId equals section.CurriculumId
                where link.InstructorId == instructorId &&
                      link.BatchId == batchId
                orderby section.Title
                select new SelectListItem
                {
                    Value = section.Id.ToString(),
                    Text = section.Title
                }
            ).ToListAsync();

            return sections;
        }

        private async Task<bool> HasAccessToBatchAsync(int instructorId, int batchId)
        {
            var today = DateTime.Today;

            var batchState = await _context.Set<Batch>()
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => new { b.IsArchived, b.EndDate })
                .FirstOrDefaultAsync();

            if (batchState == null) return false;

            // Explicit ViewBatch permission grants access to any batch (including archived)
            var hasExplicitPermission = await _context.Set<InstructorBatchPermission>()
                .AsNoTracking()
                .AnyAsync(x => x.InstructorId == instructorId &&
                               x.BatchId == batchId &&
                               x.Feature == InstructorBatchFeature.ViewBatch &&
                               x.IsGranted);
            if (hasExplicitPermission) return true;

            // Archived batches require explicit permission (already checked above)
            if (batchState.IsArchived) return false;

            if (batchState.EndDate.HasValue && batchState.EndDate.Value < today)
            {
                return await _context.Set<BatchInstructorGraduatedAccess>()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.InstructorId == instructorId &&
                        x.BatchId == batchId &&
                        x.IsActive);
            }

            var directAccess = await _context.Set<InstructorBatchRole>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == batchId);

            if (directAccess)
                return true;

            return await _context.Set<InstructorCurriculumBatch>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == batchId);
        }

        private async Task<bool> HasAccessToSectionInBatchAsync(int instructorId, int batchId, int sectionId)
        {
            var allowed = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join section in _context.Sections.AsNoTracking()
                    on link.CurriculumId equals section.CurriculumId
                where link.InstructorId == instructorId &&
                      link.BatchId == batchId &&
                      section.Id == sectionId
                select section.Id
            ).AnyAsync();

            return allowed;
        }

        private async Task<int> GetBatchCourseIdAsync(int batchId)
        {
            var courseId = await _context.Batches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => x.CourseId)
                .FirstOrDefaultAsync();

            return courseId;
        }

        private sealed class InstructorBatchOptionRaw
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = string.Empty;
        }
    }
}