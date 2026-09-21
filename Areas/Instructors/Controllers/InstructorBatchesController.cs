using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.InstructorBatches;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorBatchesController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public InstructorBatchesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        // GET: /Instructors/InstructorBatches
        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var today = DateTime.Today;

            var directBatches = await (
                from role in _context.Set<InstructorBatchRole>().AsNoTracking()
                join batch in _context.Set<Batch>().AsNoTracking()
                    on role.BatchId equals batch.Id
                join course in _context.Set<Course>().AsNoTracking()
                    on batch.CourseId equals course.Id
                where role.InstructorId == instructorId
                    && !batch.IsArchived
                    && (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select new InstructorBatchRawItem
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseTitle = course.Name,
                    AccessSource = "دور مباشر على الدفعة"
                }
            ).ToListAsync();

            var curriculumBatches = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join batch in _context.Set<Batch>().AsNoTracking()
                    on link.BatchId equals batch.Id
                join course in _context.Set<Course>().AsNoTracking()
                    on batch.CourseId equals course.Id
                where link.InstructorId == instructorId
                    && !batch.IsArchived
                    && (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select new InstructorBatchRawItem
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseTitle = course.Name,
                    AccessSource = "منهج مرتبط بالدفعة"
                }
            ).ToListAsync();

            var graduatedBatches = await (
                from access in _context.Set<BatchInstructorGraduatedAccess>().AsNoTracking()
                join batch in _context.Set<Batch>().AsNoTracking()
                    on access.BatchId equals batch.Id
                join course in _context.Set<Course>().AsNoTracking()
                    on batch.CourseId equals course.Id
                where access.InstructorId == instructorId
                    && access.IsActive
                    && !batch.IsArchived
                    && batch.EndDate.HasValue
                    && batch.EndDate.Value < today
                select new InstructorBatchRawItem
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseTitle = course.Name,
                    AccessSource = "صلاحية خاصة (دفعة تخرجت)"
                }
            ).ToListAsync();

            var allAccessibleBatches = directBatches
                .Concat(curriculumBatches)
                .Concat(graduatedBatches)
                .GroupBy(x => x.BatchId)
                .Select(g => new InstructorBatchListItemViewModel
                {
                    BatchId = g.Key,
                    BatchName = g.First().BatchName,
                    CourseTitle = g.First().CourseTitle,
                    AccessSource = string.Join(" / ", g.Select(x => x.AccessSource).Distinct()),
                    StudentsCount = 0
                })
                .OrderBy(x => x.BatchName)
                .ToList();

            if (allAccessibleBatches.Count > 0)
            {
                var studentCounts = await (
                    from enrollment in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                    join student in _context.Set<Student>().AsNoTracking()
                        on enrollment.StudentID equals student.StudentID
                    group student by enrollment.BatchId
                    into grouped
                    select new
                    {
                        BatchId = grouped.Key,
                        Count = grouped.Count()
                    }
                ).ToListAsync();

                foreach (var batch in allAccessibleBatches)
                {
                    var countItem = studentCounts.FirstOrDefault(x => x.BatchId == batch.BatchId);
                    batch.StudentsCount = countItem == null ? 0 : countItem.Count;
                }
            }

            var model = new InstructorBatchesIndexViewModel
            {
                Batches = allAccessibleBatches
            };

            return View(model);
        }

        // GET: /Instructors/InstructorBatches/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var hasAccess = await HasAccessToBatchAsync(instructorId, id);

            if (!hasAccess)
            {
                return Forbid();
            }

            var batchInfo = await (
                from batch in _context.Set<Batch>().AsNoTracking()
                join course in _context.Set<Course>().AsNoTracking()
                    on batch.CourseId equals course.Id
                where batch.Id == id
                select new
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseTitle = course.Name
                }
            ).FirstOrDefaultAsync();

            if (batchInfo == null)
            {
                return NotFound();
            }

            var students = await (
                from enrollment in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                join student in _context.Set<Student>().AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                where enrollment.BatchId == id
                orderby student.FullName
                select new InstructorBatchStudentViewModel
                {
                    StudentId = student.StudentID,
                    FullName = student.FullName,
                    NationalId = student.NationalID,
                    PhoneNumber = student.PhoneNumber,
                    Level = student.Level,
                    EnrollmentStatus = student.EnrollmentStatus
                }
            ).ToListAsync();

            var curriculums = await (
                from link in _context.Set<InstructorCurriculumBatch>().AsNoTracking()
                join curriculum in _context.Set<Curriculum>().AsNoTracking()
                    on link.CurriculumId equals curriculum.Id
                where link.InstructorId == instructorId
                      && link.BatchId == id
                orderby curriculum.Title
                select new InstructorBatchCurriculumViewModel
                {
                    CurriculumId = curriculum.Id,
                    CurriculumTitle = curriculum.Title
                }
            ).ToListAsync();

            var model = new InstructorBatchDetailsViewModel
            {
                BatchId = batchInfo.BatchId,
                BatchName = batchInfo.BatchName,
                CourseTitle = batchInfo.CourseTitle,
                StudentsCount = students.Count,
                Students = students,
                Curriculums = curriculums
            };

            return View(model);
        }

        // GET: /Instructors/InstructorBatches/Students/5
        public async Task<IActionResult> Students(int id)
        {
            var instructorId = await RequireInstructorAsync();

            if (instructorId <= 0)
            {
                return Forbid();
            }

            var hasAccess = await HasAccessToBatchAsync(instructorId, id);

            if (!hasAccess)
            {
                return Forbid();
            }

            var students = await (
                from enrollment in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                join student in _context.Set<Student>().AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                where enrollment.BatchId == id
                orderby student.FullName
                select new InstructorBatchStudentViewModel
                {
                    StudentId = student.StudentID,
                    FullName = student.FullName,
                    NationalId = student.NationalID,
                    PhoneNumber = student.PhoneNumber,
                    Level = student.Level,
                    EnrollmentStatus = student.EnrollmentStatus
                }
            ).ToListAsync();

            return View(students);
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

        private sealed class InstructorBatchRawItem
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public string AccessSource { get; set; } = string.Empty;
        }
    }
}