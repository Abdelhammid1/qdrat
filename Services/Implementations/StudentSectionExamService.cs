using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class StudentSectionExamService : IStudentSectionExamService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentSectionExamService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task CheckAndGenerateExamAsync(int studentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var activeLessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => l.Id)
                .ToListAsync();

            if (!activeLessons.Any()) return;

            var completedLessons = await _context.StudentLessonCompletions
                .Where(s => s.StudentId == studentId && activeLessons.Contains(s.LessonId))
                .Select(s => s.LessonId)
                .Distinct()
                .ToListAsync();

            if (completedLessons.Count != activeLessons.Count)
                return;

            var alreadyExists = await _context.ExamAssignments
                .AnyAsync(x => x.StudentId == studentId && x.Exam.SectionId == sectionId && x.Exam.Type == ExamType.SectionExam);

            if (alreadyExists)
                return;

            var previousQuestionIds = await _context.Homeworks
                .Where(h => h.StudentId == studentId)
                .Select(h => h.QuestionId)
                .ToListAsync();

            var candidateQuestions = await _context.Questions
                .Where(q => activeLessons.Contains(q.LessonId) && q.IsReviewed && !q.IsRejected)
                .ToListAsync();

            var uniqueQuestions = candidateQuestions
                .Where(q => !previousQuestionIds.Contains(q.Id))
                .OrderBy(q => Guid.NewGuid())
                .Take(45)
                .ToList();

            if (!uniqueQuestions.Any()) return;

            var exam = new Exam
            {
                Title = $"اختبار المحور - {DateTime.Now:yyyy/MM/dd}",
                SectionId = sectionId,
                Type = ExamType.SectionExam,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            _context.ExamAssignments.Add(new ExamAssignment
            {
                StudentId = studentId,
                ExamId = exam.Id,
                AssignedAt = DateTime.Now,
                DueDate = DateTime.Now.AddDays(3)
            });

            foreach (var q in uniqueQuestions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.Id,
                    QuestionId = q.Id
                });
            }

            await _context.SaveChangesAsync();
        }

    }
}
