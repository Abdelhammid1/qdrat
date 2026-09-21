using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.AI;
using QdratNew.ViewModels.Students;
using QdratNew.ViewModels.AI;

namespace QdratNew.Controllers.Components
{
    public class StudentChartsSidebarController : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AITipsService _aiTipsService;

        public StudentChartsSidebarController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AITipsService aiTipsService)
        {
            _context = context;
            _userManager = userManager;
            _aiTipsService = aiTipsService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return View("EmptySidebar", new StudentSidebarAnalyticsViewModel());

            var successRate = await _context.StudentAnswers
                .Where(a => a.StudentId == student.StudentID)
                .Select(a => a.IsCorrect ? 1 : 0)
                .DefaultIfEmpty()
                .AverageAsync() * 100;

            var viewModel = new StudentSidebarAnalyticsViewModel
            {
                HomeworkScoresOverTime = await _context.StudentActivityLogs
                    .Where(a => a.StudentId == student.StudentID && a.Source.Contains("Homework"))
                    .OrderBy(a => a.Timestamp)
                    .Select(a => new HomeworkScoreEntryViewModel
                    {
                        Label = a.Timestamp.ToString("dd MMM yyyy HH:mm"),
                        Score = a.Score ?? 0
                    }).ToListAsync(),

                ExamScoresOverTime = await _context.StudentActivityLogs
                    .Where(a => a.StudentId == student.StudentID && a.Source.Contains("Exam"))
                    .OrderBy(a => a.Timestamp)
                    .Select(a => new HomeworkScoreEntryViewModel
                    {
                        Label = a.Timestamp.ToString("dd MMM yyyy HH:mm"),
                        Score = a.Score ?? 0
                    }).ToListAsync(),

                OverallSuccessRate = (int)successRate,

                // ✅ استدعاء التوصيات الفعلية
                AITips = await _aiTipsService.GenerateForStudentAsync(student.StudentID)
            };

            return View("Default", viewModel);
        }
    }
}
