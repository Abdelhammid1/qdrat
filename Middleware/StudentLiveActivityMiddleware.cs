using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Services.AdminDashboard;

namespace QdratNew.Middleware
{
    public class StudentLiveActivityMiddleware
    {
        private readonly RequestDelegate _next;

        public StudentLiveActivityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db,
            IMemoryCache cache,
            IAdminLiveStudentTracker tracker)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                string path = context.Request.Path.Value ?? string.Empty;

                bool shouldTrack =
                    !string.IsNullOrWhiteSpace(path)
                    && !path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase);

                if (shouldTrack)
                {
                    string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        string cacheKey = "live-student-id-by-user-" + userId;

                        if (!cache.TryGetValue(cacheKey, out int studentId))
                        {
                            studentId = await db.Students
                                .AsNoTracking()
                                .Where(student => student.UserId == userId)
                                .Select(student => student.StudentID)
                                .FirstOrDefaultAsync();

                            if (studentId > 0)
                            {
                                cache.Set(
                                    cacheKey,
                                    studentId,
                                    new MemoryCacheEntryOptions
                                    {
                                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                                    });
                            }
                        }

                        if (studentId > 0)
                        {
                            string pageTitle = ResolvePageTitle(path);
                            tracker.Track(studentId, userId, path, pageTitle);
                        }
                    }
                }
            }

            await _next(context);
        }

        private static string ResolvePageTitle(string path)
        {
            if (path.IndexOf("StudentHomework", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Homework", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "صفحة الواجبات";
            }

            if (path.IndexOf("Exam", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "صفحة الاختبارات";
            }

            if (path.IndexOf("Remedial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "الخطة العلاجية";
            }

            if (path.IndexOf("Dashboard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "لوحة الطالب";
            }

            if (path.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "الملف الشخصي";
            }

            if (path.IndexOf("Attendance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "الحضور";
            }

            if (path.IndexOf("Question", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "صفحة سؤال";
            }

            return path;
        }
    }
}