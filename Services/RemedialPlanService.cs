using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.Data;
using QdratNew.Entities;
using Microsoft.EntityFrameworkCore;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services
{
    public class RemedialPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public RemedialPlanService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // 🔹 إنشاء خطة علاجية بناءً على تحليل الأداء
        public bool CreateRemedialPlan(int studentId, List<SectionPerformanceSummary> weakSections = null)
        {
            var studentPerformance = _context.StudentPerformances
                .Where(sp => sp.StudentID == studentId)
                .OrderByDescending(sp => sp.ExamDate)
                .FirstOrDefault();

            if (studentPerformance == null || studentPerformance.Score >= 75)
                return false;

            var weakTopics = _context.StudentProgress
                .Where(sp => sp.StudentID == studentId && sp.ProgressPercentage < 50)
                .Select(sp => sp.Topic)
                .Distinct()
                .ToList();

            if (!weakTopics.Any())
                return false;

            string performanceLevel = studentPerformance.Score switch
            {
                < 50 => "ضعيف جدًا",
                < 70 => "ضعيف",
                < 85 => "متوسط",
                _ => "جيد جدًا"
            };

            string recommendations = weakTopics.Any()
                ? $"يرجى التركيز على: {string.Join(", ", weakTopics)}"
                : "استمر في تحسين مستواك من خلال حل المزيد من الأسئلة.";

            var remedialPlan = new RemedialPlan
            {
                StudentID = studentId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                PerformanceLevel = performanceLevel,
                WeakTopics = string.Join(", ", weakTopics),
                RecommendedMaterials = "🔹 فيديوهات تدريبية، تمارين إضافية، جلسات تقوية",
                IsCompleted = false
            };

            _context.RemedialPlans.Add(remedialPlan);
            _context.SaveChanges();

            // ✅ إنشاء جلسات علاجية تلقائيًا لكل محور ضعيف
            if (weakSections != null && weakSections.Any())
            {
                int dayOffset = 1;
                foreach (var section in weakSections)
                {
                    int sessionCount = section.DifficultyLevel switch
                    {
                        "صعب" => 3,
                        "متوسط" => 2,
                        _ => 1
                    };

                    for (int i = 0; i < sessionCount; i++)
                    {
                        var session = new RemedialSession
                        {
                            StudentID = studentId,
                            SectionTitle = section.SectionTitle,
                            Subject = section.CurriculumTitle,
                            ScheduledDate = DateTime.Today.AddDays(dayOffset++),
                            StartTime = new TimeSpan(17, 0, 0),
                            EndTime = new TimeSpan(18, 0, 0),
                            RoomName = "قاعة العلاجية",
                            TotalSeats = 1,
                            ReservedSeats = 0,
                            Fee = 0,
                            IsMandatory = true,
                            IsCompleted = false
                        };

                        _context.RemedialSessions.Add(session);
                    }
                }

                _context.SaveChanges();
            }

            // ✅ إشعارات ذكية
            _notificationService.SendNotification(studentId,
                "📢 تم إنشاء خطة علاجية لك بناءً على نتائجك الأخيرة.", "خطة علاجية");

            var parent = _context.Parents
                .Include(p => p.Students)
                .FirstOrDefault(p => p.Students.Any(s => s.StudentID == studentId));

            if (parent != null)
            {
                _notificationService.SendNotification(parent.ParentID,
                    "📢 تم إنشاء خطة علاجية لابنك بناءً على أدائه الأخير.", "متابعة أولياء الأمور");
            }

            return true;
        }


        public async Task NotifyAdminForLowProgressAsync()
        {
            var lowProgressPlans = _context.RemedialPlans
       .Where(rp => (rp.IsCompleted == false || rp.IsCompleted == null)
                    && (rp.StartDate ?? DateTime.MinValue).AddDays(15) < DateTime.Now)
       .AsEnumerable()
       .Where(rp => rp.CompletionPercentage < 25.0)
       .ToList();


            if (lowProgressPlans.Any())
            {
                string message = $"⚠️ يوجد {lowProgressPlans.Count} طالبًا لم يحققوا تقدمًا كافيًا في الخطط العلاجية (أقل من 25٪).";
                await _notificationService.SendNotificationToAdminAsync(message, "متابعة الخطط العلاجية");
            }
        }

        public void CheckForLateStudents()
        {
            var latePlans = _context.RemedialPlans
     .AsEnumerable() // تحميل البيانات إلى الذاكرة قبل العمليات التي تتطلب C#
     .Where(rp =>
         rp.IsCompleted != true &&                                  // الخطة غير مكتملة أو null
         (rp.StartDate?.AddDays(15) ?? DateTime.MinValue) < DateTime.Now &&  // التاريخ بعد 15 يوم من البداية
         rp.CompletionPercentage < 50)                              // نسبة الإنجاز أقل من 50%
     .ToList();


            foreach (var plan in latePlans)
            {
                _notificationService.SendNotification(plan.StudentID, "⚠️ لم تحقق تقدمًا كافيًا في خطتك العلاجية. يرجى متابعة الدروس.", "الخطة العلاجية");
            }
        }

        public void NotifyParentsAboutStudentProgress()
        {
            var studentsWithPlans = _context.RemedialPlans
     .Include(rp => rp.Student)
     .Where(rp => rp.IsCompleted == false || rp.IsCompleted == null)
     .ToList();


            foreach (var plan in studentsWithPlans)
            {
                var parent = _context.Parents
                    .FirstOrDefault(p => p.Students.Any(s => s.StudentID == plan.StudentID));

                if (parent != null)
                {
                    string progressMessage = plan.CompletionPercentage switch
                    {
                        >= 75 => $"🎉 ابنك {plan.Student.FullName} أحرز تقدمًا رائعًا في الخطة العلاجية بنسبة {plan.CompletionPercentage}٪!",
                        >= 50 => $"✅ ابنك {plan.Student.FullName} يحرز تقدمًا جيدًا في الخطة العلاجية بنسبة {plan.CompletionPercentage}٪.",
                        >= 25 => $"⚠️ نود إبلاغك بأن ابنك {plan.Student.FullName} لم يحقق تقدمًا كبيرًا في الخطة العلاجية ({plan.CompletionPercentage}٪).",
                        _ => $"❌ ابنك {plan.Student.FullName} لم يحقق أي تقدم في الخطة العلاجية! يرجى متابعته مع إدارة المعهد."
                    };

                    _notificationService.SendNotification(parent.ParentID, progressMessage, "متابعة خطة علاجية");
                }
            }
        }
     

        public bool UpdateProgress(int studentId, int completedLessons)
        {
            var remedialPlan = _context.RemedialPlans
                               .FirstOrDefault(rp => rp.StudentID == studentId && rp.IsCompleted != true);

            if (remedialPlan == null)
                return false; // ❌ لا توجد خطة علاجية نشطة

            remedialPlan.CompletedLessons += completedLessons;

            if (remedialPlan.CompletedLessons >= remedialPlan.TotalLessons)
            {
                remedialPlan.IsCompleted = true;

                // ✅ إرسال إشعار بإتمام الخطة العلاجية
                _notificationService.SendNotification(studentId, "🎉 لقد أكملت خطتك العلاجية بنجاح! 🎉", "الخطة العلاجية");

                var parent = _context.Parents.FirstOrDefault(p => p.Students.Any(s => s.StudentID == studentId));
                if (parent != null)
                {
                    _notificationService.SendNotification(parent.ParentID, "🎉 ابنك أكمل خطته العلاجية بنجاح!", "متابعة أولياء الأمور");
                }
            }

            _context.SaveChanges();
            return true;
        }

    }
}
