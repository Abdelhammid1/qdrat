using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Common;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Reports;
using System.Text.Json;

namespace QdratNew.Areas.Instructors.Controllers.Exam
{
    [Area("Instructors")]
    public class InstructorExamStudentController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;
        public InstructorExamStudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService,
            ICacheService cacheService  )
            : base(userManager, scopeService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        // =====================================================
        // 📊 تقرير الطالب
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> StudentExamReport(int examAssignmentId, int studentId)
        {
            await RequireInstructorAsync();

            // ===============================
            // 1️⃣ التحقق من الصلاحية
            // ===============================
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.CreatedByInstructorId == CurrentInstructorId)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.DurationMinutes,
                    ExamDate = x.ScheduledDate ?? x.AssignedAt
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return NotFound();

            // ===============================
            // 2️⃣ حالة الطالب
            // ===============================
            var status = await _context.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == examAssignmentId &&
                    s.IsSubmitted);

            if (status == null)
            {
                TempData["ErrorMessage"] = "لا يوجد تقرير لهذا الطالب.";
                return RedirectToAction("Students", "InstructorExamMonitoring",
                    new { examAssignmentId });
            }

            // ===============================
            // 3️⃣ قراءة Snapshot
            // ===============================
            if (string.IsNullOrWhiteSpace(status.Note))
            {
                TempData["ErrorMessage"] = "لا يوجد بيانات تحليل.";
                return RedirectToAction("Students", "InstructorExamMonitoring",
                    new { examAssignmentId });
            }

            var snapshot =
                JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);

            if (snapshot == null)
                return NotFound();

            // ===============================
            // 4️⃣ بيانات الطالب
            // ===============================
            var student = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    s.Level
                })
                .FirstOrDefaultAsync();

            if (student == null)
                return NotFound();

            // ===============================
            // 5️⃣ حساب الزمن (خفيف)
            // ===============================
            double solveMinutes =
                snapshot.TotalTimeSeconds > 0
                    ? Math.Round(snapshot.TotalTimeSeconds / 60.0, 1)
                    : 0;

            double percentTime =
                assignment.DurationMinutes > 0
                    ? Math.Round((solveMinutes / assignment.DurationMinutes) * 100, 1)
                    : 0;

            // ===============================
            // 6️⃣ تحميل Sections مرة واحدة
            // ===============================
            var sectionIds = snapshot.Sections.Keys.ToList();

            var cacheKey = $"sections_meta_{string.Join("_", sectionIds)}";

            var sections = _cacheService.GetOrCreate(cacheKey, () =>
            {
                return _context.Sections
                    .Where(s => sectionIds.Any(id => id == s.Id))
                    .Select(s => new
                    {
                        s.Id,
                        s.Title,
                        IsQuant = s.Curriculum.IsQuantitative
                    })
                    .ToList();
            }, 30);

            // ===============================
            // 7️⃣ بناء الإحصائيات
            // ===============================
            var sectionStats = snapshot.Sections.Select(sec =>
            {
                var meta = sections.FirstOrDefault(s => s.Id == sec.Key);

                int total = sec.Value.Correct + sec.Value.Wrong + sec.Value.Skipped;

                return new ExamSectionPerformancesVm
                {
                    SectionId = sec.Key,
                    SectionName = meta?.Title ?? "غير معروف",
                    TotalQuestions = total,
                    CorrectAnswers = sec.Value.Correct,
                    WrongAnswers = sec.Value.Wrong,
                    Skipped = sec.Value.Skipped,
                    AccuracyPercent =
                        total == 0 ? 0 :
                        Math.Round(sec.Value.Correct * 100.0 / total, 1)
                };
            }).ToList();

            // ===============================
            // 8️⃣ تقسيم كمي / لفظي
            // ===============================
            var quantSections = sectionStats
                .Where(s => sections.Any(x => x.Id == s.SectionId && x.IsQuant))
                .ToList();

            var verbalSections = sectionStats
                .Where(s => sections.Any(x => x.Id == s.SectionId && !x.IsQuant))
                .ToList();

            // ===============================
            // 9️⃣ ViewModel
            // ===============================
            var vm = new ExamDetailedReportViewModel
            {
                StudentId = student.StudentID,
                StudentName = student.FullName,
                Level = student.Level,

                ExamAssignmentId = examAssignmentId,
                ExamTitle = assignment.Title,
                ExamDate = assignment.ExamDate,

                TotalQuestions = snapshot.TotalQuestions,
                TotalCorrect = snapshot.Correct,
                TotalWrong = snapshot.Wrong,
                TotalSkipped = snapshot.Skipped,

                TotalScore = snapshot.Correct,
                MaxScore = snapshot.TotalQuestions,

                SolveMinutes = solveMinutes,
                TotalMinutes = assignment.DurationMinutes,
                PercentTime = percentTime,
                OverallPercent = snapshot.ScorePercent,

                SectionPerformances = sectionStats,

                HasQuantChart = quantSections.Any(),
                HasVerbalChart = verbalSections.Any(),

                QuantLabels = quantSections.Select(s => s.SectionName).ToList(),
                QuantCorrectCounts = quantSections.Select(s => s.CorrectAnswers).ToList(),
                QuantWrongCounts = quantSections.Select(s => s.WrongAnswers).ToList(),

                VerbalLabels = verbalSections.Select(s => s.SectionName).ToList(),
                VerbalCorrectCounts = verbalSections.Select(s => s.CorrectAnswers).ToList(),
                VerbalWrongCounts = verbalSections.Select(s => s.WrongAnswers).ToList()
            };

            return View(vm);
        }
    }
}