using QdratNew.Data;
using QdratNew.Services.Instructors.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;
using QdratNew.Services.Common;

namespace QdratNew.Services.Instructors.Exams
{
    public class InstructorExamGenerationService : IInstructorExamGenerationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;

        public InstructorExamGenerationService(
            ApplicationDbContext context,
            ICacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        public GeneratedExamResult GeneratePreview(
            GenerateExamRequestVM request,
            int instructorId)
        {
            if (request == null)
                throw new InvalidOperationException("بيانات غير صحيحة");

            // ===========================
            // 1️⃣ Cache Questions Pool
            // ===========================
            var allQuestions = _cacheService.GetOrCreate("questions_pool_all", () =>
            {
                return _context.Questions
                    .Where(q =>
                        q.IsComplete &&
                        q.IsAnswerConfirmed)
                    .Select(q => new
                    {
                        q.Id,
                        q.Difficulty,
                        q.LessonId,
                        q.SectionId,
                        q.CurriculumId
                    })
                    .ToList();
            }, 10);

            if (!allQuestions.Any())
                throw new InvalidOperationException("لا توجد أسئلة");

            var result = new GeneratedExamResult
            {
                ExamTitle = request.ExamTitle
            };

            var selectedQuestions = new List<GeneratedQuestionItem>();

            // ===========================
            // 2️⃣ توزيع حسب المنهج + الصعوبة
            // ===========================
            foreach (var curriculum in request.Curriculums)
            {
                var curriculumPool = allQuestions
                    .Where(q => q.CurriculumId == curriculum.CurriculumId)
                    .ToList();

                if (!curriculumPool.Any())
                    continue;

                // 🔹 Easy
                var easy = curriculumPool
                    .Where(q => q.Difficulty == QdratNew.Enums.DifficultyLevel.Easy)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(curriculum.EasyCount)
                    .ToList();

                // 🔹 Medium
                var medium = curriculumPool
                    .Where(q => q.Difficulty == QdratNew.Enums.DifficultyLevel.Medium)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(curriculum.MediumCount)
                    .ToList();

                // 🔹 Hard
                var hard = curriculumPool
                    .Where(q => q.Difficulty == QdratNew.Enums.DifficultyLevel.Hard)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(curriculum.HardCount)
                    .ToList();

                // 🔹 VeryHard
                var veryHard = curriculumPool
                    .Where(q => q.Difficulty == QdratNew.Enums.DifficultyLevel.VeryHard)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(curriculum.VeryHardCount)
                    .ToList();

                var final = easy
                    .Concat(medium)
                    .Concat(hard)
                    .Concat(veryHard)
                    .ToList();

                foreach (var q in final)
                {
                    selectedQuestions.Add(new GeneratedQuestionItem
                    {
                        QuestionId = q.Id,
                        CurriculumId = q.CurriculumId,
                        SectionId = q.SectionId,
                        LessonId = q.LessonId
                    });
                }
            }

            if (!selectedQuestions.Any())
                throw new InvalidOperationException("لم يتم توليد أسئلة");

            // ===========================
            // 3️⃣ إزالة التكرار
            // ===========================
            result.Questions = selectedQuestions
                .GroupBy(x => x.QuestionId)
                .Select(g => g.First())
                .ToList();

            return result;
        }
    }
}