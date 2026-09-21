using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Services.AdminDashboard;

namespace QdratNew.Controllers
{
    [Authorize]
    public class StudentLiveActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IAdminLiveStudentTracker _tracker;

        public StudentLiveActivityController(
            ApplicationDbContext context,
            IMemoryCache cache,
            IAdminLiveStudentTracker tracker)
        {
            _context = context;
            _cache = cache;
            _tracker = tracker;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ping([FromForm] string? path, [FromForm] string? title)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { ok = false });
            }

            int studentId = await GetStudentIdByUserIdAsync(userId);

            if (studentId <= 0)
            {
                return Json(new { ok = false });
            }

            string safePath = string.IsNullOrWhiteSpace(path)
                ? HttpContext.Request.Headers.Referer.ToString()
                : path;

            string safeTitle = string.IsNullOrWhiteSpace(title)
                ? ResolvePageTitle(safePath)
                : title;

            _tracker.Track(studentId, userId, safePath, safeTitle);

            var snapshot = _tracker.GetSnapshotByStudentId(studentId);

            return Json(new
            {
                ok = true,
                studentId,
                trackerSeen = snapshot != null,
                isLiveNow = snapshot != null && snapshot.IsLiveNow,
                currentPath = snapshot != null ? snapshot.CurrentPath : "",
                currentPageTitle = snapshot != null ? snapshot.CurrentPageTitle : "",
                at = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Leave()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { ok = false });
            }

            int studentId = await GetStudentIdByUserIdAsync(userId);

            if (studentId <= 0)
            {
                return Json(new { ok = false });
            }

            _tracker.MarkOffline(studentId);

            return Json(new
            {
                ok = true,
                studentId
            });
        }

        private async Task<int> GetStudentIdByUserIdAsync(string userId)
        {
            string cacheKey = "live-student-id-by-user-" + userId;

            if (_cache.TryGetValue(cacheKey, out int cachedStudentId))
            {
                return cachedStudentId;
            }

            int studentId = await _context.Students
                .AsNoTracking()
                .Where(student => student.UserId == userId)
                .Select(student => student.StudentID)
                .FirstOrDefaultAsync();

            if (studentId > 0)
            {
                _cache.Set(
                    cacheKey,
                    studentId,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                    });
            }

            return studentId;
        }

        private static string ResolvePageTitle(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "صفحة غير محددة";
            }

            if (path.IndexOf("Homework", StringComparison.OrdinalIgnoreCase) >= 0)
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