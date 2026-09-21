using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class EnhancementSkillService : IEnhancementSkillService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITimeZoneService _timeZoneService;

        public EnhancementSkillService(ApplicationDbContext context, ITimeZoneService timeZoneService)
        {
            _context = context;
            _timeZoneService = timeZoneService;
        }

        public async Task<int> GenerateEnhancementSkillsAsync(int batchId, int lectureId, List<int> lessonIds, int questionsPerStudent = 5)
        {
            // 🕒 الآن بتوقيت UTC
            var nowUtc = _timeZoneService.GetNowUtc();

            // إنشاء جلسة جديدة
            var set = new EnhancementSkillSet
            {
                BatchId = batchId,
                LectureId = lectureId,
                Title = $"مهارات تعزيزية - محاضرة {lectureId}",
                CreatedAt = nowUtc
            };
            _context.EnhancementSkillSets.Add(set);
            await _context.SaveChangesAsync();

            // الطلاب
            var studentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            // الأسئلة
            var questions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId))
                .OrderBy(x => Guid.NewGuid())
                .Take(questionsPerStudent)
                .ToListAsync();

            foreach (var studentId in studentIds)
            {
                foreach (var q in questions)
                {
                    _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                    {
                        EnhancementSkillSetId = set.Id,
                        StudentId = studentId,
                        QuestionId = q.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            return set.Id;
        }

        public async Task<List<EnhancementSkillAssignment>> GetAssignmentsForStudentAsync(int studentId, int setId)
        {
            return await _context.EnhancementSkillAssignments
                .Include(a => a.Question)
                .Where(a => a.StudentId == studentId && a.EnhancementSkillSetId == setId)
                .ToListAsync();
        }

        public async Task<bool> SubmitAnswerAsync(int assignmentId, string studentAnswer)
        {
            var assignment = await _context.EnhancementSkillAssignments
                .Include(a => a.Question)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return false;

            assignment.StudentAnswer = studentAnswer;
            assignment.AnsweredAt = _timeZoneService.GetNowUtc(); // ✅ UTC ثابت
            assignment.IsCorrect = assignment.Question.CorrectAnswer == studentAnswer;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<EnhancementSkillResult> GetResultForStudentAsync(int studentId, int setId)
        {
            var assignments = await _context.EnhancementSkillAssignments
                .Where(a => a.StudentId == studentId && a.EnhancementSkillSetId == setId)
                .ToListAsync();

            var result = new EnhancementSkillResult
            {
                EnhancementSkillSetId = setId,
                StudentId = studentId,
                TotalQuestions = assignments.Count,
                CorrectAnswers = assignments.Count(a => a.IsCorrect == true),
                CompletedAt = _timeZoneService.GetNowUtc() // ✅ UTC ثابت
            };

            _context.EnhancementSkillResults.Add(result);
            await _context.SaveChangesAsync();

            return result;
        }
    }
}
