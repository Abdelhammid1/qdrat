using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.Services.Students.Abstractions;
using QdratNew.ViewModels.Students;
using System.Text.Json;

namespace QdratNew.Services.Students
{
    public class StudentCourseContext : IStudentCourseContext
    {
        private const string ContextKey = "StudentCourseContext";

        private readonly IHttpContextAccessor _http;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentCourseContext(
            IHttpContextAccessor http,
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _http = http;
            _contextFactory = contextFactory;
        }

        // =========================
        // قراءة القيم النشطة
        // =========================
        public int? ActiveCourseId
        {
            get
            {
                var ctx = ReadContext();
                return ctx?.CourseId;
            }
        }

        public int? ActiveBatchId
        {
            get
            {
                var ctx = ReadContext();
                return ctx?.BatchId;
            }
        }

        // =========================
        // تعيين السياق
        // =========================
        public void SetSelection(int courseId, int batchId)
        {
            using var db = _contextFactory.CreateDbContext();

            var ctx = (
                from b in db.Batches
                join c in db.Courses on b.CourseId equals c.Id
                where b.Id == batchId && c.Id == courseId
                select new StudentCourseContextVm
                {
                    CourseId = c.Id,
                    CourseTitle = c.Name,
                    BatchId = b.Id,
                    BatchTitle = b.Name
                }
            ).FirstOrDefault();

            if (ctx == null)
                return;

            _http.HttpContext!.Session.SetString(
                ContextKey,
                JsonSerializer.Serialize(ctx)
            );
        }

        // =========================
        // مسح السياق
        // =========================
        public void ClearSelection()
        {
            _http.HttpContext?.Session.Remove(ContextKey);
        }

        public bool HasSelection()
        {
            return ReadContext() != null;
        }

        // =========================
        // Helper
        // =========================
        private StudentCourseContextVm? ReadContext()
        {
            var json = _http.HttpContext?.Session.GetString(ContextKey);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<StudentCourseContextVm>(json);
        }
    }
}
