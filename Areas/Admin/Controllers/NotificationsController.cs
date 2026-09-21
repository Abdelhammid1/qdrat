using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Admin.ViewModels;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Admin.Notifications;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        // ✅ تحديد الإشعار كمقروء
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
                return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // ✅ حذف الإشعارات الأقدم من 60 يومًا
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOldNotifications()
        {
            var oldNotifications = await _context.Notifications
                .Where(n => n.SentAt < DateTime.Now.AddDays(-60))
                .ToListAsync();

            _context.Notifications.RemoveRange(oldNotifications);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ✅ عرض جميع الإشعارات مع التصفية
        [HttpGet]
        public async Task<IActionResult> Index(string category = "All", bool? isRead = null)
        {
            var baseQuery = _context.Notifications.AsNoTracking();
            var filteredQuery = baseQuery;

            if (category != "All")
            {
                if (Enum.TryParse(category, out NotificationCategory parsedCategory))
                {
                    filteredQuery = filteredQuery.Where(n => n.Category == parsedCategory);
                }
            }

            if (isRead.HasValue)
                filteredQuery = filteredQuery.Where(n => n.IsRead == isRead.Value);

            var notifications = await filteredQuery
                .OrderByDescending(n => n.SentAt)
                .Take(200)
                .Select(n => new AdminNotificationListItemViewModel
                {
                    NotificationId = n.NotificationId,
                    Message = n.Message,
                    SentAt = n.SentAt,
                    IsRead = n.IsRead,
                    Category = n.Category,
                    CategoryName = GetCategoryDisplayName(n.Category),
                    TargetUrl = n.TargetUrl,
                    RecipientName = n.User != null
                        ? (n.User.FullName ?? n.User.UserName ?? n.User.Email ?? "مستخدم")
                        : n.Student != null
                            ? n.Student.FullName
                            : n.Parent != null
                                ? n.Parent.FullName
                                : n.StudentID == 0
                                    ? "الإدارة"
                                    : "غير محدد",
                    RecipientType = n.UserId != null
                        ? "مستخدم"
                        : n.StudentID != null && n.StudentID > 0
                            ? "طالب"
                            : n.ParentID != null
                                ? "ولي أمر"
                                : n.StudentID == 0
                                    ? "إدارة"
                                    : "عام"
                })
                .ToListAsync();

            var model = new AdminNotificationsIndexViewModel
            {
                SelectedCategory = category,
                IsReadFilter = isRead,
                TotalCount = await baseQuery.CountAsync(),
                UnreadCount = await baseQuery.CountAsync(n => !n.IsRead),
                TodayUnreadCount = await baseQuery.CountAsync(n => !n.IsRead && n.SentAt.Date == DateTime.Today),
                Categories = Enum.GetValues<NotificationCategory>()
                    .Select(c => new AdminNotificationCategoryOptionViewModel
                    {
                        Value = c.ToString(),
                        Text = GetCategoryDisplayName(c)
                    })
                    .ToList(),
                Notifications = notifications
            };

            return View(model);
        }


        [HttpPost]
        public IActionResult SendNotification(int studentId, string message, string category)
        {
            if (!Enum.TryParse(category, out NotificationCategory parsedCategory))
            {
                ModelState.AddModelError("Category", "التصنيف غير صحيح!");
                return RedirectToAction("Index"); // أو إرجاع `View` مع رسالة خطأ
            }

            var notification = new Notification
            {
                StudentID = studentId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = parsedCategory // ✅ الآن التصنيف صحيح
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }


        [HttpPost]
        public IActionResult SendBulkNotification(string message)
        {
            var students = _context.Students.ToList(); // جلب جميع الطلاب

            foreach (var student in students)
            {
                var notification = new Notification
                {
                    StudentID = student.StudentID,
                    Message = $"📢 إشعار عام: {message}",
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult SendCourseNotification(int courseId, string message)
        {
            var studentsInCourse = _context.Students
                .Where(s => _context.StudentCourses.Any(sc => sc.StudentID == s.StudentID && sc.CourseId == courseId))
                .ToList();

            foreach (var student in studentsInCourse)
            {
                var notification = new Notification
                {
                    StudentID = student.StudentID,
                    Message = $"📢 تحديث لدورة #{courseId}: {message}",
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }



        [HttpPost]
        public IActionResult SendExamReminder(int studentId, string subject, DateTime examDate)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = $"📢 لديك اختبار في مادة {subject} بتاريخ {examDate.ToShortDateString()}. استعد جيدًا!",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
        [HttpPost]
        public IActionResult SendAssignmentDeadline(int studentId, string subject, DateTime dueDate)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = $"📌 لديك واجب في مادة {subject} يجب تسليمه قبل {dueDate.ToShortDateString()}. لا تتأخر!",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
        [HttpPost]
        public IActionResult SendCourseUpdate(int studentId, string courseName)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = $"📚 تم تحديث محتوى دورة {courseName}. تأكد من مراجعة التعديلات الجديدة!",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }


        // داخل NotificationsController.cs في Admin Area

        [HttpGet]
        public async Task<IActionResult> SmartNotifications()
        {
            var totalStudents = await _context.Students.CountAsync();

            var viewModel = new SmartNotificationViewModel
            {
                StudentsWithUnsolvedLastHomework = await _context.Homeworks
                    .Where(h => h.IsSent)
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.OrderByDescending(h => h.AssignedAt).First().IsCompleted == false)
                    .CountAsync(),

                StudentsLateTwoHomeworks = await _context.Homeworks
                    .Where(h => h.IsSent)
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.OrderByDescending(h => h.AssignedAt).Take(2).Count(h => !h.IsCompleted) >= 2)
                    .CountAsync(),

                StudentsDidNotSolveThisWeek = await _context.Homeworks
                    .Where(h => h.IsSent && h.AssignedAt >= DateTime.Now.AddDays(-7))
                    .GroupBy(h => h.StudentId)
                    .Where(g => g.All(h => !h.IsCompleted))
                    .CountAsync(),

                StudentsDidNotStartRemedialPlan = await _context.RemedialPlans
                    .Where(p => !p.Sessions.Any())
                    .CountAsync()
            };

            return View(viewModel);
        }







        [HttpPost]
        public IActionResult SendMotivationalMessage(int studentId, string message)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = $"🎉 تهانينا! {message}",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // ─── Create Notification ────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = await BuildCreateViewModel();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateNotificationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var refreshed = await BuildCreateViewModel();
                refreshed.TargetType = model.TargetType;
                refreshed.Message = model.Message;
                refreshed.Category = model.Category;
                refreshed.MeetingAt = model.MeetingAt;
                return View(refreshed);
            }

            var notifications = new List<Notification>();
            var sentAt = DateTime.Now;

            switch (model.TargetType)
            {
                case "Admins":
                    if (model.SelectedAdminUserIds == null || !model.SelectedAdminUserIds.Any())
                    {
                        ModelState.AddModelError("", "يجب اختيار مدير واحد على الأقل");
                        return View(await BuildCreateViewModel());
                    }
                    foreach (var userId in model.SelectedAdminUserIds)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = userId,
                            Message = model.Message,
                            Category = model.Category,
                            MeetingAt = model.MeetingAt,
                            SentAt = sentAt,
                            IsRead = false
                        });
                    }
                    break;

                case "Students":
                    if (model.SelectedStudentIds == null || !model.SelectedStudentIds.Any())
                    {
                        ModelState.AddModelError("", "يجب اختيار طالب واحد على الأقل");
                        return View(await BuildCreateViewModel());
                    }
                    foreach (var studentId in model.SelectedStudentIds)
                    {
                        notifications.Add(new Notification
                        {
                            StudentID = studentId,
                            Message = model.Message,
                            Category = model.Category,
                            MeetingAt = model.MeetingAt,
                            SentAt = sentAt,
                            IsRead = false
                        });
                    }
                    break;

                case "Instructors":
                    if (model.SelectedInstructorIds == null || !model.SelectedInstructorIds.Any())
                    {
                        ModelState.AddModelError("", "يجب اختيار مدرب واحد على الأقل");
                        return View(await BuildCreateViewModel());
                    }
                    var instructors = await _context.Instructors
                        .Where(i => model.SelectedInstructorIds.Contains(i.Id) && i.UserId != null)
                        .ToListAsync();
                    foreach (var instructor in instructors)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = instructor.UserId,
                            Message = model.Message,
                            Category = model.Category,
                            MeetingAt = model.MeetingAt,
                            SentAt = sentAt,
                            IsRead = false
                        });
                    }
                    break;

                default:
                    ModelState.AddModelError("", "نوع المستلم غير صحيح");
                    return View(await BuildCreateViewModel());
            }

            if (notifications.Count == 0)
            {
                ModelState.AddModelError("", "لم يتم إنشاء أي إشعار، تأكد من صحة البيانات");
                return View(await BuildCreateViewModel());
            }

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم إرسال {notifications.Count} إشعار بنجاح";
            return RedirectToAction("Index");
        }

        // AJAX: جلب طلاب الدفعة
        [HttpGet]
        public async Task<IActionResult> GetStudentsByBatch(int batchId)
        {
            var students = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == batchId && e.Status == "Active")
                .Select(e => new
                {
                    id = e.StudentID,
                    name = e.Student.FullName,
                    nationalId = e.Student.NationalID
                })
                .OrderBy(s => s.name)
                .ToListAsync();

            return Json(students);
        }

        private async Task<CreateNotificationViewModel> BuildCreateViewModel()
        {
            var categories = Enum.GetValues<NotificationCategory>()
                .Select(c => new NotificationCategorySelectItem
                {
                    Value = c.ToString(),
                    Text = GetCategoryDisplayName(c)
                }).ToList();

            // الإداريين: المستخدمون في أدوار الإدارة
            var adminRoleNames = new[] { "Admin", "SuperAdmin", "Manager" };
            var adminUserIds = new HashSet<string>();
            foreach (var role in adminRoleNames)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                    adminUserIds.Add(u.Id);
            }

            var adminUsers = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id) && u.IsActive)
                .Select(u => new AdminUserCheckboxItem
                {
                    UserId = u.Id,
                    FullName = u.FullName ?? u.UserName ?? u.Email ?? "مجهول",
                    Email = u.Email
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // الدفعات النشطة
            var batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived && b.IsActive)
                .OrderByDescending(b => b.StartDate)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{b.Name} ({b.Course.Name})"
                })
                .ToListAsync();

            // المدربين النشطين
            var instructors = await _context.Instructors
                .Where(i => i.IsActive)
                .Select(i => new InstructorCheckboxItem
                {
                    InstructorId = i.Id,
                    FullName = i.FullName,
                    Specialization = i.Specialization
                })
                .OrderBy(i => i.FullName)
                .ToListAsync();

            return new CreateNotificationViewModel
            {
                Categories = categories,
                AdminUsers = adminUsers,
                Batches = batches,
                Instructors = instructors
            };
        }

        private static string GetCategoryDisplayName(NotificationCategory category)
        {
            var member = typeof(NotificationCategory).GetMember(category.ToString()).FirstOrDefault();
            return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? category.ToString();
        }
    }
}
