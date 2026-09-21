using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;
using QdratNew.ViewModels.AI;

namespace QdratNew.ViewComponents
{
    public class StudentChartsSidebar : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentChartsSidebar(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null)
                return View("EmptySidebar", new StudentSidebarAnalyticsViewModel());

            var successRate = await _context.StudentAnswers
                .Where(a => a.StudentId == student.StudentID)
                .Select(a => a.IsCorrect == true ? 1 : 0)
                .DefaultIfEmpty()
                .AverageAsync() * 100;

            var homeworkScores = await _context.StudentActivityLogs
                .Where(a => a.StudentId == student.StudentID &&
                            a.Source == "Homework" &&
                            a.ActivityType == "حل واجب كامل")
                .OrderBy(a => a.Timestamp)
                .Select(a => new HomeworkScoreEntryViewModel
                {
                    Label = a.Timestamp.ToString("dd MMM yyyy HH:mm"),
                    Score = a.Score ?? 0
                })
                .ToListAsync();

            var examScores = await _context.StudentActivityLogs
                .Where(a => a.StudentId == student.StudentID &&
                            a.Source == "Exam" &&
                            a.ActivityType == "حل اختبار كامل")
                .OrderBy(a => a.Timestamp)
                .Select(a => new HomeworkScoreEntryViewModel
                {
                    Label = a.Timestamp.ToString("dd MMM yyyy HH:mm"),
                    Score = a.Score ?? 0
                })
                .ToListAsync();

            var viewModel = new StudentSidebarAnalyticsViewModel
            {
                HomeworkScoresOverTime = homeworkScores,
                ExamScoresOverTime = examScores,
                OverallSuccessRate = (int)successRate,
                AITips = new List<SmartRecommendation>
{
    new SmartRecommendation { Title = "مراجعة المجموعات", Description = "نسبة الخطأ فيه 70%" },
    new SmartRecommendation { Title = "أداء متميز", Description = "أداءك في اختبار الوحدة الأخيرة ممتاز 🔥" },
    new SmartRecommendation { Title = "ننصح بمراجعة المتوسطات", Description = "راجع المتوسطات التراكمية لتحسين مستواك" }
}

            };

            return View("Default", viewModel);
        }
    }
}
