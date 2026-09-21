using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Instructors.Models;

namespace QdratNew.Services.Instructors.Implementations
{
    public class LectureInstructorSyncService : ILectureInstructorSyncService
    {
        private readonly ApplicationDbContext _context;

        public LectureInstructorSyncService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> SyncAutoLecturesAsync(int curriculumId, int batchId, int newInstructorId)
        {
            // نفس نمط الـ join المستخدم في InstructorLecturesController وBatchLectureAutoGenerationService
            var lecturesToUpdate = await (
                from lecture in _context.Lecture
                join section in _context.Sections on lecture.SectionId equals section.Id
                where lecture.BatchId == batchId
                    && section.CurriculumId == curriculumId
                    && lecture.InstructorAssignmentSource == LectureInstructorAssignmentSource.Auto
                    && lecture.InstructorId != newInstructorId
                select lecture
            ).ToListAsync();

            if (lecturesToUpdate.Count == 0)
                return 0;

            // تجميع التعديلات أولاً ثم حفظ واحد (قاعدة AGENTS.md: لا SaveChanges داخل loop)
            foreach (var lecture in lecturesToUpdate)
            {
                lecture.InstructorId = newInstructorId;
            }

            await _context.SaveChangesAsync();

            return lecturesToUpdate.Count;
        }

        public async Task<LectureSyncStatus> GetSyncStatusAsync(int curriculumId, int batchId, int instructorId)
        {
            var totalAutoLectures = await (
                from lecture in _context.Lecture.AsNoTracking()
                join section in _context.Sections.AsNoTracking() on lecture.SectionId equals section.Id
                where lecture.BatchId == batchId
                    && section.CurriculumId == curriculumId
                    && lecture.InstructorAssignmentSource == LectureInstructorAssignmentSource.Auto
                select lecture.Id
            ).CountAsync();

            var syncedCount = await (
                from lecture in _context.Lecture.AsNoTracking()
                join section in _context.Sections.AsNoTracking() on lecture.SectionId equals section.Id
                where lecture.BatchId == batchId
                    && section.CurriculumId == curriculumId
                    && lecture.InstructorAssignmentSource == LectureInstructorAssignmentSource.Auto
                    && lecture.InstructorId == instructorId
                select lecture.Id
            ).CountAsync();

            return new LectureSyncStatus
            {
                TotalAutoLectures = totalAutoLectures,
                SyncedCount = syncedCount
            };
        }
    }
}
