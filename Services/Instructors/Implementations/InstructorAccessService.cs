using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;

namespace QdratNew.Services.Instructors.Implementations
{
    public class InstructorAccessService : IInstructorAccessService
    {
        private readonly ApplicationDbContext _context;

        public InstructorAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // هل مدرب شريك؟
        // =====================================================
        public async Task<bool> IsPartnerInstructorAsync(string userId)
        {
            return await _context.Instructors
                .Where(x => x.UserId == userId)
                .Select(x => x.IsPartnerInstructor)
                .FirstOrDefaultAsync();
        }

        // =====================================================
        // جلب InstructorId
        // =====================================================
        public async Task<int?> GetInstructorIdAsync(string userId)
        {
            return await _context.Instructors
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        // =====================================================
        // الدفعات المسموحة
        // =====================================================
        public async Task<List<int>> GetAccessibleBatchIdsAsync(string userId)
        {
            var today = DateTime.Today;
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted);

            if (instructor == null)
                return new List<int>();

            // =============================
            // مدرب شريك
            // =============================
            if (instructor.IsPartnerInstructor)
            {
                var teachingBatches = await _context.InstructorCurriculumBatches
                    .Where(x =>
                        x.InstructorId == instructor.Id &&
                        x.Batch != null &&
                        x.Batch.IsActive &&
                        !x.Batch.IsDeleted &&
                        (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                    .Select(x => x.BatchId)
                    .ToListAsync();

                var roleBatches = await _context.InstructorBatchRoles
                    .Where(x =>
                        x.InstructorId == instructor.Id &&
                        x.Batch != null &&
                        x.Batch.IsActive &&
                        !x.Batch.IsDeleted &&
                        (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                    .Select(x => x.BatchId)
                    .ToListAsync();

                return teachingBatches
                    .Union(roleBatches)
                    .Distinct()
                    .ToList();
            }

            // =============================
            // مدرب منصة
            // =============================

            // 1️⃣ جلب CourseIds
            var courseIds = await _context.CourseInstructors
                .Where(x => x.InstructorID == instructor.Id)
                .Select(x => x.CourseID)
                .Distinct()
                .ToListAsync();

            // 2️⃣ جلب كل الباتشات مرة واحدة
            var allBatches = await _context.Batches
                .Where(b =>
                    b.IsActive &&
                    !b.IsDeleted &&
                    (!b.EndDate.HasValue || b.EndDate.Value >= today))
                .Select(b => new { b.Id, b.CourseId })
                .ToListAsync();

            // 3️⃣ تصفية في الذاكرة (توافق SQL 2014)
            return allBatches
                .Where(b => courseIds.Contains(b.CourseId))
                .Select(b => b.Id)
                .Distinct()
                .ToList();
        }
        // =====================================================
        // المناهج المسموحة
        // =====================================================
        public async Task<List<int>> GetAccessibleCurriculumIdsAsync(string userId)
        {
            var today = DateTime.Today;
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (instructor == null)
                return new List<int>();

            if (instructor.IsPartnerInstructor)
            {
                return await _context.InstructorCurriculumBatches
                    .Where(x =>
                        x.InstructorId == instructor.Id &&
                        x.Batch != null &&
                        x.Batch.IsActive &&
                        !x.Batch.IsDeleted &&
                        (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today))
                    .Select(x => x.CurriculumId)
                    .Distinct()
                    .ToListAsync();
            }

            return await _context.CurriculumInstructors
                .Where(x => x.InstructorId == instructor.Id)
                .Select(x => x.CurriculumId)
                .Distinct()
                .ToListAsync();
        }

        // =====================================================
        // هل له دور معين داخل دفعة؟
        // =====================================================
        public async Task<bool> HasRoleInBatchAsync(
            string userId,
            int batchId,
            InstructorBatchRoleType roleType)
        {
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (instructor == null)
                return false;

            if (roleType == InstructorBatchRoleType.Teaching)
            {
                return await _context.InstructorCurriculumBatches
                    .AnyAsync(x =>
                        x.InstructorId == instructor.Id &&
                        x.BatchId == batchId &&
                        x.Batch != null &&
                        x.Batch.IsActive &&
                        !x.Batch.IsDeleted &&
                        (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= DateTime.Today));
            }

            return await _context.InstructorBatchRoles
                .AnyAsync(x =>
                    x.InstructorId == instructor.Id &&
                    x.BatchId == batchId &&
                    x.RoleType == roleType &&
                    x.Batch != null &&
                    x.Batch.IsActive &&
                    !x.Batch.IsDeleted &&
                    (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= DateTime.Today));
        }

        // =====================================================
        // هل يستطيع تعديل بنك الأسئلة؟
        // =====================================================
        public async Task<bool> CanEditQuestionBankAsync(string userId)
        {
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (instructor == null)
                return false;

            // مدرب المنصة فقط
            return !instructor.IsPartnerInstructor;
        }
    }

}
