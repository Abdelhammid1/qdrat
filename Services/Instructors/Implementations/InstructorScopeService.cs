using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Instructors.Implementations
{
    // ⚠️ Epic B1 (PROMPT/Diagnose_CourseInstructor_Dependency.sql): تم تشغيل الاستعلام التشخيصي
    // على قاعدة البيانات الفعلية وأعاد صفر صفوف — لا يوجد أي مدرب نشط حاليًا يعتمد على
    // CourseInstructors وحده دون ربط مطابق عبر InstructorCurriculumBatches أو InstructorBatchRoles.
    // القرار (Epic B2): CourseInstructors مهجور الكتابة (لا Controller يكتب فيه) ويُبقى للقراءة فقط
    // للتوافق التاريخي دون حذف — أعد تشغيل الاستعلام قبل أي تنظيف مستقبلي فعلي لهذا الجدول.
    public class InstructorScopeService : IInstructorScopeService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public InstructorScopeService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================
        // Instructor
        // =========================================
        public async Task<Instructor> GetInstructorByUserIdAsync(string userId)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted && x.IsActive);
        }

        // =========================================
        // Batches (المصدر الصحيح)
        // =========================================
        public async Task<List<int>> GetAllowedBatchIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            var activeBatchIds = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseMembershipBatchIds = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on courseInstructor.CourseID equals batch.CourseId
                where courseInstructor.InstructorID == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var roleBatchIds = await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                where role.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var graduatedBatchIds = await context.BatchInstructorGraduatedAccesses
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.IsActive &&
                    !x.Batch.IsArchived &&
                    x.Batch.EndDate.HasValue &&
                    x.Batch.EndDate.Value < today)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            return activeBatchIds
                .Union(courseMembershipBatchIds)
                .Union(roleBatchIds)
                .Union(graduatedBatchIds)
                .Distinct()
                .ToList();
        }

        // =========================================
        // Courses (from actual instructor scope)
        // =========================================
        public async Task<List<int>> GetAllowedCourseIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var coursesFromInstructorMembership = await (
                from link in context.CourseInstructors.AsNoTracking()
                join course in context.Courses.AsNoTracking()
                    on link.CourseID equals course.Id
                where link.InstructorID == instructorId &&
                      course.IsActive
                select course.Id
            ).Distinct().ToListAsync();

            var coursesFromCurriculumBatches = await (
                from link in context.InstructorCurriculumBatches.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on link.BatchId equals batch.Id
                join course in context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                where link.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today) &&
                      course.IsActive
                select course.Id
            ).Distinct().ToListAsync();

            var coursesFromBatchRoles = await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                join course in context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                where role.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today) &&
                      course.IsActive
                select course.Id
            ).Distinct().ToListAsync();

            return coursesFromInstructorMembership
                .Union(coursesFromCurriculumBatches)
                .Union(coursesFromBatchRoles)
                .Distinct()
                .ToList();
        }

        // =========================================
        // Curriculums (from actual instructor scope)
        // =========================================
        public async Task<List<int>> GetAllowedCurriculumIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            var directCurriculumIds = await context.CurriculumInstructors
                .AsNoTracking()
                .Where(x => x.InstructorId == instructorId)
                .Select(x => x.CurriculumId)
                .Distinct()
                .ToListAsync();

            var curriculumBatchIds = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.CurriculumId)
                .Distinct()
                .ToListAsync();

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseCurriculumIds = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join courseCurriculum in context.CourseCurriculums.AsNoTracking()
                    on courseInstructor.CourseID equals courseCurriculum.CourseId
                join course in context.Courses.AsNoTracking()
                    on courseInstructor.CourseID equals course.Id
                where courseInstructor.InstructorID == instructorId &&
                      course.IsActive
                select courseCurriculum.CurriculumId
            ).Distinct().ToListAsync();

            var roleCurriculumIds = await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                join courseCurriculum in context.CourseCurriculums.AsNoTracking()
                    on batch.CourseId equals courseCurriculum.CourseId
                where role.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select courseCurriculum.CurriculumId
            ).Distinct().ToListAsync();

            return directCurriculumIds
                .Union(curriculumBatchIds)
                .Union(courseCurriculumIds)
                .Union(roleCurriculumIds)
                .Distinct()
                .ToList();
        }

        // =========================================
        // Students (بدون Contains في SQL)
        // =========================================
        public async Task<List<int>> GetAllowedStudentIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            // 🟢 1) جلب الدفعات من المصدر الصحيح
            var activeBatchIds = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseMembershipBatchIds = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on courseInstructor.CourseID equals batch.CourseId
                where courseInstructor.InstructorID == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var roleBatchIds = await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                where role.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var graduatedBatchIds = await context.BatchInstructorGraduatedAccesses
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.IsActive &&
                    !x.Batch.IsArchived &&
                    x.Batch.EndDate.HasValue &&
                    x.Batch.EndDate.Value < today)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            var batchIds = activeBatchIds
                .Union(courseMembershipBatchIds)
                .Union(roleBatchIds)
                .Union(graduatedBatchIds)
                .Distinct()
                .ToList();

            if (batchIds.Count == 0)
                return new List<int>();

            // 🟢 2) تحميل كل الطلاب
            var enrollments = await context.StudentBatchEnrollments
                .AsNoTracking()
                .ToListAsync();

            // 🟢 3) فلترة في الذاكرة
            return enrollments
                .Where(x => batchIds.Contains(x.BatchId))
                .Select(x => x.StudentID)
                .Distinct()
                .ToList();
        }

        // =========================================
        // Check Batch
        // =========================================
        public async Task<bool> CanAccessBatchAsync(int instructorId, int batchId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            var activeAccess = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == batchId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today));

            if (activeAccess) return true;

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseMembershipAccess = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on courseInstructor.CourseID equals batch.CourseId
                where courseInstructor.InstructorID == instructorId &&
                      batch.Id == batchId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).AnyAsync();

            if (courseMembershipAccess) return true;

            var roleAccess = await context.InstructorBatchRoles
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == batchId &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today));

            if (roleAccess) return true;

            return await context.BatchInstructorGraduatedAccesses
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == batchId &&
                    x.IsActive &&
                    !x.Batch.IsArchived);
        }

        public async Task<bool> CanAccessCourseAsync(int instructorId, int courseId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var directCourseAccess = await context.CourseInstructors
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorID == instructorId &&
                    x.CourseID == courseId &&
                    x.Course.IsActive);

            if (directCourseAccess)
                return true;

            var curriculumBatchAccess = await (
                from link in context.InstructorCurriculumBatches.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on link.BatchId equals batch.Id
                where link.InstructorId == instructorId &&
                      batch.CourseId == courseId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select link.Id
            ).AnyAsync();

            if (curriculumBatchAccess)
                return true;

            return await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                where role.InstructorId == instructorId &&
                      batch.CourseId == courseId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select role.Id
            ).AnyAsync();
        }

        public async Task<bool> CanAccessCurriculumAsync(int instructorId, int curriculumId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            var directCurriculumAccess = await context.CurriculumInstructors
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.CurriculumId == curriculumId);

            if (directCurriculumAccess)
                return true;

            var curriculumBatchAccess = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.CurriculumId == curriculumId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today));

            if (curriculumBatchAccess)
                return true;

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseAccess = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join courseCurriculum in context.CourseCurriculums.AsNoTracking()
                    on courseInstructor.CourseID equals courseCurriculum.CourseId
                join course in context.Courses.AsNoTracking()
                    on courseInstructor.CourseID equals course.Id
                where courseInstructor.InstructorID == instructorId &&
                      courseCurriculum.CurriculumId == curriculumId &&
                      course.IsActive
                select courseCurriculum.Id
            ).AnyAsync();

            if (courseAccess)
                return true;

            return await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                join courseCurriculum in context.CourseCurriculums.AsNoTracking()
                    on batch.CourseId equals courseCurriculum.CourseId
                where role.InstructorId == instructorId &&
                      courseCurriculum.CurriculumId == curriculumId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select role.Id
            ).AnyAsync();
        }

        // =========================================
        // Direct curriculum-batch assignments (للداشبورد)
        // =========================================
        public async Task<List<int>> GetDirectBatchIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            return await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<int>> GetDirectCurriculumIdsAsync(int instructorId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            return await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.CurriculumId)
                .Distinct()
                .ToListAsync();
        }

        // =========================================
        // Permission-based batch access
        // =========================================
        public async Task<List<int>> GetPermittedBatchIdsAsync(int instructorId, InstructorBatchFeature feature)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.InstructorBatchPermissions
                .AsNoTracking()
                .Where(x => x.InstructorId == instructorId && x.Feature == feature && x.IsGranted)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();
        }

        // =========================================
        // Check Student
        // =========================================
        public async Task<bool> CanAccessStudentAsync(int instructorId, int studentId)
        {
            using var context = _contextFactory.CreateDbContext();
            var today = DateTime.Today;

            // 🟢 1) جلب الدفعات
            var activeBids = await context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    !x.Batch.IsArchived &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            // ⚠️ CourseInstructors مهجور الكتابة — انظر تعليق Epic B1/B2 أعلى الكلاس
            var courseMembershipBids = await (
                from courseInstructor in context.CourseInstructors.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on courseInstructor.CourseID equals batch.CourseId
                where courseInstructor.InstructorID == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var roleBids = await (
                from role in context.InstructorBatchRoles.AsNoTracking()
                join batch in context.Batches.AsNoTracking()
                    on role.BatchId equals batch.Id
                where role.InstructorId == instructorId &&
                      batch.IsActive &&
                      !batch.IsDeleted &&
                      !batch.IsArchived &&
                      (!batch.EndDate.HasValue || batch.EndDate.Value >= today)
                select batch.Id
            ).Distinct().ToListAsync();

            var graduatedBids = await context.BatchInstructorGraduatedAccesses
                .AsNoTracking()
                .Where(x =>
                    x.InstructorId == instructorId &&
                    x.IsActive &&
                    !x.Batch.IsArchived &&
                    x.Batch.EndDate.HasValue &&
                    x.Batch.EndDate.Value < today)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();

            var batchIds = activeBids
                .Union(courseMembershipBids)
                .Union(roleBids)
                .Union(graduatedBids)
                .Distinct()
                .ToList();

            if (batchIds.Count == 0)
                return false;

            // 🟢 2) جلب تسجيل الطالب
            var enrollments = await context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.StudentID == studentId)
                .ToListAsync();

            // 🟢 3) فلترة في الذاكرة
            return enrollments.Any(x => batchIds.Contains(x.BatchId));
        }
    }
}
