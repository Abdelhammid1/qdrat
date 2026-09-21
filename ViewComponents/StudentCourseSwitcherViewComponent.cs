using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.ViewModels.Students;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.ViewComponents
{
    public class StudentCourseSwitcherViewComponent : ViewComponent
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentCourseSwitcherViewComponent(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            using var db = _contextFactory.CreateDbContext();

            var studentIdObj = HttpContext.Items["StudentId"];
            if (studentIdObj == null)
                return Content(string.Empty);

            var studentId = (int)studentIdObj;

            // =========================
            // قراءة الـ Context الحالي
            // =========================
            int activeCourseId = 0;
            int activeBatchId = 0;

            var json = HttpContext.Session.GetString("StudentCourseContext");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var ctx = JsonSerializer.Deserialize<StudentCourseContextVm>(json);
                if (ctx != null)
                {
                    activeCourseId = ctx.CourseId;
                    activeBatchId = ctx.BatchId;
                }
            }

            // =========================
            // جلب كل اشتراكات الطالب
            // =========================
            var items = await (
                from e in db.StudentBatchEnrollments
                join b in db.Batches on e.BatchId equals b.Id
                join c in db.Courses on b.CourseId equals c.Id
                where e.StudentID == studentId
                select new StudentCourseSwitchItemVm
                {
                    CourseId = c.Id,
                    CourseTitle = c.Name,
                    BatchId = b.Id,
                    BatchTitle = b.Name,
                    IsActive = (c.Id == activeCourseId && b.Id == activeBatchId)
                }
            ).AsNoTracking().ToListAsync();

            if (items.Count == 0)
                return Content(string.Empty);

            return View(items);
        }
    }
}
