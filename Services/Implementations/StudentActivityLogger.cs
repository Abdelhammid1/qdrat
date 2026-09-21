using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class StudentActivityLogger : IStudentActivityLogger
    {
        private readonly ApplicationDbContext _context;

        public StudentActivityLogger(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(int studentId,
                             string activityType,
                             string activityTitle,
                             string source,
                             bool? wasCorrect = null,
                             int? score = null,
                             string? note = null,
                             int? lessonId = null,
                             int? sectionId = null)
        {
            var log = new StudentActivityLog
            {
                StudentId = studentId,
                ActivityType = activityType,
                ActivityTitle = activityTitle,
                Source = source,
                WasCorrect = wasCorrect,
                Score = score,
                Note = note,
                Timestamp = DateTime.UtcNow,
                LessonId = lessonId,
                SectionId = sectionId
            };

            _context.StudentActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

    }
}
