using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Students;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QdratNew.ViewComponents
{
    public class StudentBatchIconsViewComponent : ViewComponent
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentBatchIconsViewComponent(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var studentIdObj = HttpContext.Items["StudentId"];
            if (studentIdObj == null)
                return Content(string.Empty);

            int studentId = (int)studentIdObj;

            using var db = _contextFactory.CreateDbContext();

            // =========================
            // قراءة الفلتر الحالي للداشبورد
            // =========================
            int? activeBatchId =
                HttpContext.Session.GetInt32("DashboardBatchFilter");

            // =========================
            // جلب دفعات الطالب
            // =========================
            var items = await (
                from e in db.StudentBatchEnrollments
                join b in db.Batches on e.BatchId equals b.Id
                join c in db.Courses on b.CourseId equals c.Id
                where e.StudentID == studentId
                select new StudentBatchIconVm
                {
                    BatchId = b.Id,
                    BatchTitle = b.Name,
                    CourseTitle = c.Name,
                    IsActive = activeBatchId.HasValue
                        ? b.Id == activeBatchId.Value
                        : false
                }
            ).AsNoTracking().ToListAsync();

            if (items.Count <= 1)
                return Content(string.Empty);

            return View(items);
        }
    }
}
