using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class CourseExamGeneratorService : ICourseExamGeneratorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ISystemSettingService _systemSettingService;

        public CourseExamGeneratorService(IDbContextFactory<ApplicationDbContext> contextFactory, ISystemSettingService systemSettingService)
        {
            _contextFactory = contextFactory;
            _systemSettingService = systemSettingService;
        }

        public async Task CheckAndGenerateCourseExamAsync(int batchId, int courseId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var curriculumIds = await _context.CourseCurriculums
      .Where(cc => cc.CourseId == courseId)
      .Select(cc => cc.CurriculumId)
      .ToListAsync();


            if (!curriculumIds.Any()) return;

            var completedCurricula = await _context.ExamAssignmentsToBatches
                .Where(e => e.BatchId == batchId && e.IsSentToStudents && e.SectionId == null && e.CurriculumId != null && curriculumIds.Contains(e.CurriculumId.Value))
                .Select(e => e.CurriculumId.Value)
                .Distinct()
                .ToListAsync();

            if (completedCurricula.Count < curriculumIds.Count)
                return;

            var exists = await _context.ExamAssignmentsToBatches
                .AnyAsync(e => e.BatchId == batchId && e.CurriculumId == null && e.SectionId == null);

            if (exists)
                return;

            var lessonIds = await _context.Lessons
                .Where(l => l.Unit.SectionUnits.Any(su => curriculumIds.Contains(su.Section.CurriculumId)))
                .Select(l => l.Id)
                .ToListAsync();

            var questions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId)
                            && q.IsComplete && q.IsReviewed && !q.IsRejected
                            && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .OrderBy(q => Guid.NewGuid())
                .ToListAsync();

            if (questions.Count < 60) return;

            // ✅ توزيع الأسئلة على 3 أقسام فقط إن كانت الدورة تحتوي منهجين بالضبط
            if (curriculumIds.Count == 2)
            {
                var sharedQuant = await _systemSettingService.GetIntAsync("FinalExamSharedQuantitativeCount", 10);
                var sharedVerbal = await _systemSettingService.GetIntAsync("FinalExamSharedVerbalCount", 10);
                var perCurriculum = await _systemSettingService.GetIntAsync("FinalExamPerCurriculumQuestionCount", 20);

                var sharedQuestions = questions
                    .Where(q => q.IsQuantitative)
                    .Take(sharedQuant)
                    .Concat(questions.Where(q => !q.IsQuantitative).Take(sharedVerbal))
                    .ToList();

                var curriculum1Questions = questions
                    .Where(q => q.CurriculumId == curriculumIds[0])
                    .Take(perCurriculum)
                    .ToList();

                var curriculum2Questions = questions
                    .Where(q => q.CurriculumId == curriculumIds[1])
                    .Take(perCurriculum)
                    .ToList();

                var ordered = new List<ExamQuestion>();
                int order = 1;

                ordered.AddRange(sharedQuestions.Select(q => new ExamQuestion
                {
                    QuestionId = q.Id,
                    Order = order++,
                    IsManuallySelected = false
                }));

                ordered.AddRange(curriculum1Questions.Select(q => new ExamQuestion
                {
                    QuestionId = q.Id,
                    Order = order++,
                    IsManuallySelected = false
                }));

                ordered.AddRange(curriculum2Questions.Select(q => new ExamQuestion
                {
                    QuestionId = q.Id,
                    Order = order++,
                    IsManuallySelected = false
                }));

                var assignment = new ExamAssignmentToBatch
                {
                    BatchId = batchId,
                    CurriculumId = null,
                    SectionId = null,
                    Title = "📗 اختبار شامل على الدورة",
                    TotalQuestions = ordered.Count,
                    DurationMinutes = 90,
                    IsSentToStudents = true,
                    CreatedAt = DateTime.Now,
                    AssignedAt = DateTime.Now,
                    Questions = ordered
                };

                _context.ExamAssignmentsToBatches.Add(assignment);
                await _context.SaveChangesAsync();
                return;
            }

            // ✅ إن لم تكن دورة ثنائية، استخدم المنطق الأصلي كما هو
            var fallbackAssignment = new ExamAssignmentToBatch
            {
                BatchId = batchId,
                CurriculumId = null,
                SectionId = null,
                Title = "📗 اختبار شامل على الدورة",
                TotalQuestions = questions.Count,
                DurationMinutes = 90,
                IsSentToStudents = true,
                CreatedAt = DateTime.Now,
                AssignedAt = DateTime.Now,
                Questions = questions
                    .Take(questions.Count)
                    .Select((q, index) => new ExamQuestion
                    {
                        QuestionId = q.Id,
                        Order = index + 1,
                        IsManuallySelected = false
                    }).ToList()
            };

            _context.ExamAssignmentsToBatches.Add(fallbackAssignment);
            await _context.SaveChangesAsync();
        }
    }
}
