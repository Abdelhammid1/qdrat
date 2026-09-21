using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class StudentExamDashboardController : StudentBaseController
    {

        private readonly IStudentExamStatusService _examStatusService;

        public StudentExamDashboardController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
                IStudentExamStatusService examStatusService,
            UserManager<ApplicationUser> userManager)
            : base(contextFactory, userManager)
        {

            _examStatusService = examStatusService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var summary = await _examStatusService.GetStatusSummaryAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            using var db = _contextFactory.CreateDbContext();

            // ==================================================
            // Curriculums (كما هي)
            // ==================================================
            var curriculumes = await (
                from cc in db.CourseCurriculums.AsNoTracking()
                join c in db.Curriculums.AsNoTracking()
                    on cc.CurriculumId equals c.Id
                where cc.CourseId == ActiveCourseId
                select new CurriculumFilterViewModel
                {
                    Id = c.Id,
                    Title = c.Title
                }
            ).Distinct().ToListAsync();

            var vm = new StudentExamDashboardViewModel
            {
                TotalAssigned = summary.Total,
                Completed = summary.Solved,
                Pending = summary.Required,
                Late = summary.Late,

                WeaknessCount = 0,
                MistakesCount = 0,

                Curriculumes = curriculumes,
                Recommendations = new()
            };

            return View(vm);
        }

        // ==================================================
        // (سيُستكمل لاحقًا) بيانات التشارت حسب المنهج
        // ==================================================
        [HttpGet]
        public async Task<IActionResult> GetDashboardDataByCurriculum(int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = StudentId;
            var batchId = ActiveBatchId;
            var courseId = ActiveCourseId;

            // ==================================================
            // 1) محاولات الطالب (Radar + Exam Progress)
            // ==================================================
            var studentAttempts = await (
                from qa in db.QuestionAttemptNew.AsNoTracking()
                join ea in db.ExamAssignmentsToBatches.AsNoTracking()
                    on qa.ExamAssignmentId equals ea.Id
                join e in db.Exams.AsNoTracking()
                    on ea.ExamId equals e.Id
                join q in db.Questions.AsNoTracking()
                    on qa.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking()
                    on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking()
                    on l.SectionId equals s.Id
                join c in db.Curriculums.AsNoTracking()
                    on s.CurriculumId equals c.Id
                join b in db.Batches.AsNoTracking()
                    on ea.BatchId equals b.Id
                where qa.StudentId == studentId
                      && ea.BatchId == batchId
                      && b.CourseId == courseId
                select new
                {
                    ExamId = e.Id,
                    ExamTitle = e.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    CurriculumId = c.Id,
                    IsCorrect = qa.IsCorrect
                }
            ).ToListAsync();

            if (curriculumId > 0)
                studentAttempts = studentAttempts
                    .Where(x => x.CurriculumId == curriculumId)
                    .ToList();

            // ==================================================
            // 2) Radar – أداء الطالب
            // ==================================================
            var studentRadar = studentAttempts
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x => x.IsCorrect == true);

                    return new
                    {
                        SectionId = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
                        StudentScore = total == 0
                            ? 0
                            : Math.Round(correct * 100.0 / total, 2)
                    };
                })
                .ToList();

            // ==================================================
            // 3) محاولات الدفعة (لحساب متوسط المحاور)
            // ==================================================
            var batchAttempts = await (
                from qa in db.QuestionAttemptNew.AsNoTracking()
                join ea in db.ExamAssignmentsToBatches.AsNoTracking()
                    on qa.ExamAssignmentId equals ea.Id
                join q in db.Questions.AsNoTracking()
                    on qa.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking()
                    on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking()
                    on l.SectionId equals s.Id
                join c in db.Curriculums.AsNoTracking()
                    on s.CurriculumId equals c.Id
                join b in db.Batches.AsNoTracking()
                    on ea.BatchId equals b.Id
                where ea.BatchId == batchId
                      && b.CourseId == courseId
                select new
                {
                    SectionId = s.Id,
                    CurriculumId = c.Id,
                    StudentId = qa.StudentId,
                    IsCorrect = qa.IsCorrect
                }
            ).ToListAsync();

            if (curriculumId > 0)
                batchAttempts = batchAttempts
                    .Where(x => x.CurriculumId == curriculumId)
                    .ToList();

            // متوسط كل طالب في كل محور
            var batchStudentSectionScores = batchAttempts
                .GroupBy(x => new { x.SectionId, x.StudentId })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x => x.IsCorrect == true);

                    return new
                    {
                        SectionId = g.Key.SectionId,
                        StudentScore = total == 0
                            ? 0
                            : correct * 100.0 / total
                    };
                })
                .ToList();

            // متوسط الدفعة لكل محور
            var batchSectionAverages = batchStudentSectionScores
                .GroupBy(x => x.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key,
                    AverageScore = Math.Round(g.Average(x => x.StudentScore), 2)
                })
                .ToList();

            // ==================================================
            // 4) تجهيز Radar Arrays
            // ==================================================
            var sectionIds     = studentRadar.Select(x => x.SectionId).ToArray();
            var sectionLabels  = studentRadar.Select(x => x.SectionTitle).ToArray();
            var studentScores  = studentRadar.Select(x => x.StudentScore).ToArray();

            var batchAverages = studentRadar
                .Select(x =>
                    batchSectionAverages
                        .FirstOrDefault(b => b.SectionId == x.SectionId)?.AverageScore ?? 0
                )
                .ToArray();

            // ==================================================
            // 5) Exam Progress + Comparison (كما هو سابقًا)
            // ==================================================
            var examGroups = studentAttempts
                .GroupBy(x => new { x.ExamId, x.ExamTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x => x.IsCorrect == true);

                    return new
                    {
                        ExamId = g.Key.ExamId,
                        ExamTitle = g.Key.ExamTitle,
                        TotalQuestions = total,
                        CorrectAnswers = correct,
                        StudentScore = total == 0
                            ? 0
                            : Math.Round(correct * 100.0 / total, 2)
                    };
                })
                .ToList();

            var examTitles = examGroups.Select(x => x.ExamTitle).ToArray();
            var correctAnswers = examGroups.Select(x => x.CorrectAnswers).ToArray();
            var remainingQuestions = examGroups
                .Select(x => Math.Max(0, x.TotalQuestions - x.CorrectAnswers))
                .ToArray();

            var studentExamScores = examGroups.Select(x => x.StudentScore).ToArray();

            // (متوسط الدفعة للاختبارات – محسوب سابقًا عندك)
      



            // ==================================================
            // حساب متوسط الدفعة لكل اختبار (📊 Exam Comparison)
            // ==================================================
            var batchExamAttempts = await (
                from qa in db.QuestionAttemptNew.AsNoTracking()
                join ea in db.ExamAssignmentsToBatches.AsNoTracking()
                    on qa.ExamAssignmentId equals ea.Id
                join e in db.Exams.AsNoTracking()
                    on ea.ExamId equals e.Id
                join b in db.Batches.AsNoTracking()
                    on ea.BatchId equals b.Id
                where ea.BatchId == batchId
                      && b.CourseId == courseId
                select new
                {
                    ExamId = e.Id,
                    StudentId = qa.StudentId,
                    IsCorrect = qa.IsCorrect
                }
            ).ToListAsync();

            // متوسط كل طالب في كل اختبار
            var batchStudentExamScores = batchExamAttempts
                .GroupBy(x => new { x.ExamId, x.StudentId })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x => x.IsCorrect == true);

                    return new
                    {
                        ExamId = g.Key.ExamId,
                        StudentScore = total == 0
                            ? 0
                            : correct * 100.0 / total
                    };
                })
                .ToList();

            // متوسط الدفعة لكل اختبار
            var batchExamAverages = batchStudentExamScores
                .GroupBy(x => x.ExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    AverageScore = Math.Round(g.Average(x => x.StudentScore), 2)
                })
                .ToList();


            var batchExamScores = examGroups
          .Select(x =>
              batchExamAverages
                  .FirstOrDefault(b => b.ExamId == x.ExamId)?.AverageScore ?? 0
          )
          .ToArray();



            // ==================================================
            // 7) توليد التوصيات (Logic بسيط – بدون DB)
            // ==================================================
            var recommendations = new List<string>();

            // محاور ضعيفة
            foreach (var sec in studentRadar.Where(x => x.StudentScore < 40))
            {
                recommendations.Add(
                    $"يُنصح بالتركيز على محور \"{sec.SectionTitle}\" حيث يظهر أن مستواك فيه يحتاج إلى تحسين."
                );
            }

            // محاور قوية
            foreach (var sec in studentRadar.Where(x => x.StudentScore >= 80))
            {
                recommendations.Add(
                    $"أداؤك ممتاز في محور \"{sec.SectionTitle}\"، استمر على هذا المستوى."
                );
            }

            // تذبذب عام
            if (studentRadar.Any())
            {
                var max = studentRadar.Max(x => x.StudentScore);
                var min = studentRadar.Min(x => x.StudentScore);

                if (max >= 80 && min <= 30)
                {
                    recommendations.Add(
                        "مستواك غير متوازن بين المحاور، حاول توزيع مجهودك بشكل أفضل."
                    );
                }
            }

            // أداء اختبارات منخفض
            if (studentExamScores.Any())
            {
                var avgExam = studentExamScores.Average();
                if (avgExam < 50)
                {
                    recommendations.Add(
                        "متوسط أدائك في الاختبارات منخفض، ننصح بمراجعة أخطائك السابقة قبل التقدم."
                    );
                }
            }



            // ==================================================
            // 6) JSON النهائي
            // ==================================================
            return Json(new
            {
                sectionIds,
                sectionLabels,
                studentScores,
                batchAverages,

                examTitles,
                correctAnswers,
                remainingQuestions,
                studentExamScores,
                batchExamScores,
                recommendations
            });
        }


    }
}
