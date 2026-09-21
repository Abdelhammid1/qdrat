using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Students.Exams;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentIndividualExamsController : StudentBaseController
    {
        public StudentIndividualExamsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager
        ) : base(contextFactory, userManager)
        {
        }

        // =========================================
        // 📌 عرض الاختبارات الفردية فقط
        // =========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            using var db = _contextFactory.CreateDbContext();

            var exams = await (
        from a in db.ExamAssignmentsToStudents.AsNoTracking()
        join e in db.Exams.AsNoTracking()
            on a.ExamId equals e.Id

        join s in db.ExamStudentStatuses.AsNoTracking()
            .Where(x =>
                x.StudentId == StudentId &&
                x.IsSubmitted &&
                x.Status == ExamStatus.Completed)
            on a.Id equals s.ExamAssignmentToStudentId into sj

        from status in sj.DefaultIfEmpty()

        where a.StudentId == StudentId

        select new StudentExamListItemVm
        {
            ExamAssignmentId = a.Id,
            Title = e.Title,
            DurationMinutes = a.DurationMinutes,
            ScheduledDate = a.ScheduledDate,
            EndAt = a.EndAt,
            IsCompleted = status != null,
            IsParentRequest = a.SourceType == "ParentRequest"
        }
    ).ToListAsync();


            return View("Index", exams);
        }
    }
}
