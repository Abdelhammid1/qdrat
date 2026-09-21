using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Dashboard;

namespace QdratNew.Services.AdminDashboard
{
    public class AdminOperationsDrillDownService : IAdminOperationsDrillDownService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdminLiveStudentTracker _liveTracker;

        private const int DefaultLectureDurationMinutes = 120;
        private const int LowAttendanceThreshold = 70;

        public AdminOperationsDrillDownService(
            ApplicationDbContext context,
            IAdminLiveStudentTracker liveTracker)
        {
            _context = context;
            _liveTracker = liveTracker;
        }

        public async Task<AdminDrillDownPageViewModel> GetActiveStudentsAsync()
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);

            List<LiveStudentSnapshot> liveSnapshots = _liveTracker.GetSnapshots();

            List<ActiveStudentProjection> allActiveStudents = await (
                from enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                join student in _context.Students.AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                join batch in _context.Batches.AsNoTracking()
                    on enrollment.BatchId equals batch.Id
                where (student.EnrollmentStatus == "نشط" || student.EnrollmentStatus == "Active")
                      && (enrollment.Status == "Active" || enrollment.Status == "نشط")
                select new ActiveStudentProjection
                {
                    StudentId = student.StudentID,
                    FullName = student.FullName,
                    NationalId = student.NationalID,
                    PhoneNumber = student.PhoneNumber,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    LastLoginAt = student.LastLoginAt,
                    UserId = student.UserId
                })
                .OrderBy(item => item.FullName)
                .ToListAsync();

            List<ActiveStudentProjection> visibleStudents = new List<ActiveStudentProjection>();

            foreach (ActiveStudentProjection student in allActiveStudents)
            {
                bool loggedToday =
                    student.LastLoginAt.HasValue
                    && student.LastLoginAt.Value >= todayStart
                    && student.LastLoginAt.Value < tomorrowStart;

                bool hasLiveSnapshot = false;

                foreach (LiveStudentSnapshot snapshot in liveSnapshots)
                {
                    if (snapshot.StudentId == student.StudentId)
                    {
                        hasLiveSnapshot = true;
                        break;
                    }
                }

                if (loggedToday || hasLiveSnapshot)
                {
                    visibleStudents.Add(student);
                }
            }

            List<ActiveStudentProjection> distinctStudents = visibleStudents
                .GroupBy(item => item.StudentId)
                .Select(group => group.First())
                .OrderByDescending(item => item.LastLoginAt.HasValue ? item.LastLoginAt.Value : DateTime.MinValue)
                .ToList();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "تفاصيل الطلاب النشطين اليوم",
                Subtitle = "يعرض الطلاب الذين سجلوا دخول اليوم أو تم التقاط حركتهم مباشرة داخل المنصة، ويميز بين المتحرك فعليًا، الموجود بدون نشاط، وآخر صفحة كان عليها الطالب.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا يوجد طلاب سجلوا دخول اليوم أو لم يتم التقاط حركة طالب حتى الآن."
            };

            int liveNowCount = 0;
            int movingCount = 0;
            int idleCount = 0;
            int loggedTodayCount = 0;

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "student", Header = "الطالب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "lastLogin", Header = "آخر دخول" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "liveStatus", Header = "الحالة الآن" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "currentPage", Header = "الصفحة الحالية" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "lastSeen", Header = "آخر نشاط" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "moves", Header = "عدد التنقلات" });

            foreach (ActiveStudentProjection student in distinctStudents)
            {
                LiveStudentSnapshot? snapshot = null;

                foreach (LiveStudentSnapshot item in liveSnapshots)
                {
                    if (item.StudentId == student.StudentId)
                    {
                        snapshot = item;
                        break;
                    }
                }

                bool loggedToday =
                    student.LastLoginAt.HasValue
                    && student.LastLoginAt.Value >= todayStart
                    && student.LastLoginAt.Value < tomorrowStart;

                if (loggedToday)
                {
                    loggedTodayCount++;
                }

                bool isLiveNow = snapshot != null && snapshot.IsLiveNow;
                bool isMoving = snapshot != null && snapshot.IsMoving;

                string statusText = "مسجل دخول ولكن لا ينفذ شيء";
                string rowCss = "idle";

                if (isMoving)
                {
                    statusText = "نشط ويتنقل فعليًا";
                    rowCss = "success";
                    movingCount++;
                    liveNowCount++;
                }
                else if (isLiveNow)
                {
                    statusText = "موجود الآن بدون تنقل واضح";
                    rowCss = "warning";
                    liveNowCount++;
                    idleCount++;
                }
                else
                {
                    idleCount++;
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = rowCss,
                    ActionUrl = "/Admin/Students/Details/" + student.StudentId,
                    ActionText = "ملف الطالب",
                    StudentId  = student.StudentId,
                    UserId     = CleanRouteValue(student.UserId),
                    EntityName = student.FullName,
                    Cells = new Dictionary<string, string>
            {
                { "student", student.FullName },
                { "batch", student.BatchName },
                { "lastLogin", student.LastLoginAt.HasValue ? student.LastLoginAt.Value.ToString("yyyy/MM/dd HH:mm") : "لم يتم تحديث LastLoginAt" },
                { "liveStatus", statusText },
                { "currentPage", snapshot != null ? snapshot.CurrentPageTitle : "لا توجد حركة مسجلة بعد الدخول" },
                { "lastSeen", snapshot != null ? snapshot.LastSeenAt.ToString("HH:mm:ss") : "-" },
                { "moves", snapshot != null ? snapshot.PageHitCount.ToString() : "0" }
            }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "إجمالي ظاهر الآن",
                Value = distinctStudents.Count.ToString(),
                Hint = "LastLoginAt أو Live Tracker",
                CssClass = "info"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "سجلوا دخول اليوم",
                Value = loggedTodayCount.ToString(),
                Hint = "حسب LastLoginAt",
                CssClass = "info"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "نشطون الآن",
                Value = liveNowCount.ToString(),
                Hint = "آخر نشاط خلال 5 دقائق",
                CssClass = "success"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "يتنقلون فعليًا",
                Value = movingCount.ToString(),
                Hint = "تغيرت الصفحة داخل الجلسة",
                CssClass = "success"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "بدون نشاط واضح",
                Value = idleCount.ToString(),
                Hint = "دخلوا ولم تظهر حركة جديدة",
                CssClass = "warning"
            });

            return model;
        }

        private static string CleanRouteValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('"', '\'');
        }
        public async Task<AdminDrillDownPageViewModel> GetRunningLecturesAsync()
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);

            List<LectureDetailsProjection> lectures = await (
                from lecture in _context.Lecture.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                join instructor in _context.Instructors.AsNoTracking()
                    on lecture.InstructorId equals instructor.Id
                join section in _context.Sections.AsNoTracking()
                    on lecture.SectionId equals section.Id
                where lecture.Date >= todayStart
                      && lecture.Date < tomorrowStart
                select new LectureDetailsProjection
                {
                    LectureId = lecture.Id,
                    Title = lecture.Title,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    InstructorId = instructor.Id,
                    InstructorName = instructor.FullName,
                    SectionTitle = section.Title,
                    StartAt = lecture.Date,
                    EndAt = lecture.Date.AddMinutes(DefaultLectureDurationMinutes),
                    Location = lecture.Location
                })
                .OrderBy(item => item.StartAt)
                .ToListAsync();

            List<AttendanceProjection> attendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(record => record.RecordedAt >= todayStart && record.RecordedAt < tomorrowStart)
                .Select(record => new AttendanceProjection
                {
                    LectureId = record.LectureId,
                    StudentId = record.StudentId,
                    IsPresent = record.IsPresent
                })
                .ToListAsync();

            List<StudentBatchProjection> studentBatches = await LoadStudentBatchesAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "تفاصيل المحاضرات الجارية الآن",
                Subtitle = "يعرض المحاضرات داخل نافذة التشغيل الحالية Date + ساعتين، مع نسبة حضور كل محاضرة وإجراء المتابعة.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد محاضرات جارية الآن."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "lecture", Header = "المحاضرة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "instructor", Header = "المدرب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "section", Header = "المحور" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "time", Header = "الوقت" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "attendance", Header = "الحضور" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "status", Header = "الحالة" });

            int runningCount = 0;
            int lowAttendanceCount = 0;

            foreach (LectureDetailsProjection lecture in lectures)
            {
                if (!(lecture.StartAt <= now && lecture.EndAt >= now))
                {
                    continue;
                }

                runningCount++;

                int totalStudents = studentBatches.Count(item => item.BatchId == lecture.BatchId && item.IsActive);

                int presentStudents = attendance
                    .Where(item => item.LectureId == lecture.LectureId && item.IsPresent)
                    .Select(item => item.StudentId)
                    .Distinct()
                    .Count();

                double attendancePercent = CalculatePercent(presentStudents, totalStudents);

                string status = "مستقرة";
                string rowCss = "success";

                if (attendancePercent < LowAttendanceThreshold)
                {
                    status = "حضور منخفض يحتاج تدخل";
                    rowCss = "warning";
                    lowAttendanceCount++;
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = rowCss,
                    ActionUrl = "/Admin/Lectures/Details/" + lecture.LectureId,
                    ActionText = "فتح المحاضرة",
                    Cells = new Dictionary<string, string>
                    {
                        { "lecture", lecture.Title },
                        { "batch", lecture.BatchName },
                        { "instructor", lecture.InstructorName },
                        { "section", lecture.SectionTitle },
                        { "time", lecture.StartAt.ToString("HH:mm") + " - " + lecture.EndAt.ToString("HH:mm") },
                        { "attendance", presentStudents + " / " + totalStudents + " - " + attendancePercent.ToString("0.##") + "%" },
                        { "status", status }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "محاضرات جارية",
                Value = runningCount.ToString(),
                Hint = "داخل الوقت الحالي",
                CssClass = "info"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "حضور منخفض",
                Value = lowAttendanceCount.ToString(),
                Hint = "أقل من 70%",
                CssClass = "warning"
            });

            return model;
        }

        public async Task<AdminDrillDownPageViewModel> GetOpenExamsAsync()
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);

            List<ExamProjection> exams = await (
                from assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on assignment.BatchId equals batch.Id
                where assignment.ScheduledDate.HasValue
                      && assignment.ScheduledDate.Value >= todayStart
                      && assignment.ScheduledDate.Value < tomorrowStart
                      && assignment.IsSentToStudents
                select new ExamProjection
                {
                    ExamAssignmentId = assignment.Id,
                    Title = assignment.Title,
                    BatchName = batch.Name,
                    ScheduledDate = assignment.ScheduledDate.Value,
                    DurationMinutes = assignment.DurationMinutes,
                    TotalQuestions = assignment.TotalQuestions,
                    IsOnline = assignment.IsOnline,
                    IsInLab = assignment.IsInLab
                })
                .OrderBy(item => item.ScheduledDate)
                .ToListAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "تفاصيل الاختبارات المفتوحة اليوم",
                Subtitle = "يعرض الاختبارات المجدولة والمرسلة للطلاب، مع تمييز المفتوح الآن.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد اختبارات مفتوحة اليوم."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "title", Header = "الاختبار" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "time", Header = "الوقت" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "questions", Header = "الأسئلة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "type", Header = "النوع" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "status", Header = "الحالة" });

            int openNow = 0;

            foreach (ExamProjection exam in exams)
            {
                DateTime endAt = exam.ScheduledDate.AddMinutes(exam.DurationMinutes);

                bool isOpenNow = exam.ScheduledDate <= now && endAt >= now;

                if (isOpenNow)
                {
                    openNow++;
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = isOpenNow ? "success" : "normal",
                    ActionUrl = "/Admin/ExamAssignments/Details/" + exam.ExamAssignmentId,
                    ActionText = "فتح الاختبار",
                    Cells = new Dictionary<string, string>
                    {
                        { "title", exam.Title },
                        { "batch", exam.BatchName },
                        { "time", exam.ScheduledDate.ToString("HH:mm") + " - " + endAt.ToString("HH:mm") },
                        { "questions", exam.TotalQuestions.ToString() },
                        { "type", exam.IsOnline ? "أونلاين" : exam.IsInLab ? "حضوري" : "غير محدد" },
                        { "status", isOpenNow ? "مفتوح الآن" : "مجدول اليوم" }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "اختبارات اليوم",
                Value = exams.Count.ToString(),
                Hint = "مرسلة للطلاب",
                CssClass = "info"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "مفتوحة الآن",
                Value = openNow.ToString(),
                Hint = "داخل نافذة الوقت",
                CssClass = "success"
            });

            return model;
        }

        public async Task<AdminDrillDownPageViewModel> GetTodayAttendanceAsync()
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);

            List<AttendanceDetailsProjection> rows = await (
                from record in _context.AttendanceRecords.AsNoTracking()
                join student in _context.Students.AsNoTracking()
                    on record.StudentId equals student.StudentID
                join lecture in _context.Lecture.AsNoTracking()
                    on record.LectureId equals lecture.Id
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                where record.RecordedAt >= todayStart
                      && record.RecordedAt < tomorrowStart
                select new AttendanceDetailsProjection
                {
                    StudentId = student.StudentID,
                    StudentName = student.FullName,
                    BatchName = batch.Name,
                    LectureId = lecture.Id,
                    LectureTitle = lecture.Title,
                    IsPresent = record.IsPresent,
                    RecordedAt = record.RecordedAt,
                    Notes = record.Notes
                })
                .OrderByDescending(item => item.RecordedAt)
                .ToListAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "تفاصيل حضور اليوم",
                Subtitle = "يعرض سجلات الحضور المسجلة اليوم لكل محاضرة وطالب.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد سجلات حضور اليوم."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "student", Header = "الطالب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "lecture", Header = "المحاضرة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "recordedAt", Header = "وقت التسجيل" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "status", Header = "الحالة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "notes", Header = "ملاحظات" });

            int present = 0;
            int absent = 0;

            foreach (AttendanceDetailsProjection item in rows)
            {
                if (item.IsPresent)
                {
                    present++;
                }
                else
                {
                    absent++;
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = item.IsPresent ? "success" : "danger",
                    ActionUrl = "/Admin/Students/Details/" + item.StudentId,
                    ActionText = "ملف الطالب",
                    Cells = new Dictionary<string, string>
                    {
                        { "student", item.StudentName },
                        { "batch", item.BatchName },
                        { "lecture", item.LectureTitle },
                        { "recordedAt", item.RecordedAt.ToString("HH:mm") },
                        { "status", item.IsPresent ? "حاضر" : "غائب" },
                        { "notes", item.Notes ?? "-" }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "إجمالي السجلات",
                Value = rows.Count.ToString(),
                Hint = "اليوم",
                CssClass = "info"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "حضور",
                Value = present.ToString(),
                Hint = "IsPresent = true",
                CssClass = "success"
            });

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "غياب",
                Value = absent.ToString(),
                Hint = "IsPresent = false",
                CssClass = "danger"
            });

            return model;
        }

        public async Task<AdminDrillDownPageViewModel> GetClosingHomeworksAsync()
        {
            DateTime now = DateTime.Now;
            DateTime within24Hours = now.AddHours(24);

            List<HomeworkProjection> rows = await (
                from homeworkSet in _context.HomeworkSets.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on homeworkSet.BatchId equals batch.Id
                where homeworkSet.EndAt.HasValue
                      && homeworkSet.EndAt.Value >= now
                      && homeworkSet.EndAt.Value <= within24Hours
                      && homeworkSet.IsSent
                select new HomeworkProjection
                {
                    HomeworkSetId = homeworkSet.Id,
                    Title = homeworkSet.Title,
                    BatchName = batch.Name,
                    CreatedAt = homeworkSet.CreatedAt,
                    EndAt = homeworkSet.EndAt.Value,
                    IsSent = homeworkSet.IsSent
                })
                .OrderBy(item => item.EndAt)
                .ToListAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "الواجبات التي تغلق خلال 24 ساعة",
                Subtitle = "يعرض الواجبات المرسلة التي اقترب موعد إغلاقها لاتخاذ إجراء سريع.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد واجبات تغلق خلال 24 ساعة."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "title", Header = "الواجب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "createdAt", Header = "تاريخ الإرسال" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "endAt", Header = "يغلق في" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "remaining", Header = "المتبقي" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "status", Header = "الحالة" });

            foreach (HomeworkProjection item in rows)
            {
                TimeSpan remaining = item.EndAt.Subtract(now);

                string remainingText = remaining.TotalHours >= 1
                    ? Math.Floor(remaining.TotalHours).ToString("0") + " ساعة"
                    : Math.Max(0, remaining.Minutes).ToString("0") + " دقيقة";

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = remaining.TotalHours <= 3 ? "danger" : "warning",
                    ActionUrl = "/Admin/HomeworkManagement/Details/" + item.HomeworkSetId,
                    ActionText = "فتح الواجب",
                    Cells = new Dictionary<string, string>
                    {
                        { "title", item.Title },
                        { "batch", item.BatchName },
                        { "createdAt", item.CreatedAt.ToString("yyyy/MM/dd") },
                        { "endAt", item.EndAt.ToString("yyyy/MM/dd HH:mm") },
                        { "remaining", remainingText },
                        { "status", remaining.TotalHours <= 3 ? "حرج" : "قريب الإغلاق" }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "واجبات قريبة الإغلاق",
                Value = rows.Count.ToString(),
                Hint = "خلال 24 ساعة",
                CssClass = "warning"
            });

            return model;
        }

        public async Task<AdminDrillDownPageViewModel> GetBatchHealthAsync(int? batchId)
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);
            DateTime weekStart = todayStart.AddDays(-6);

            List<BatchProjection> batchesQuery = await _context.Batches
                .AsNoTracking()
                .Select(batch => new BatchProjection
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                })
                .OrderBy(batch => batch.BatchName)
                .ToListAsync();

            List<StudentBatchProjection> studentBatches = await LoadStudentBatchesAsync();

            List<AttendanceProjection> todayAttendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(record => record.RecordedAt >= todayStart && record.RecordedAt < tomorrowStart)
                .Select(record => new AttendanceProjection
                {
                    LectureId = record.LectureId,
                    StudentId = record.StudentId,
                    IsPresent = record.IsPresent
                })
                .ToListAsync();

            List<HomeworkSetStudentProjection> homeworkRows = await (
                from homeworkSetStudent in _context.HomeworkSetStudents.AsNoTracking()
                join homeworkSet in _context.HomeworkSets.AsNoTracking()
                    on homeworkSetStudent.HomeworkSetId equals homeworkSet.Id
                where homeworkSet.CreatedAt >= weekStart
                      && homeworkSet.CreatedAt < tomorrowStart
                select new HomeworkSetStudentProjection
                {
                    BatchId = homeworkSet.BatchId,
                    StudentId = homeworkSetStudent.StudentId,
                    IsSubmitted = homeworkSetStudent.IsSubmitted
                })
                .ToListAsync();

            List<ExamStatusProjection> examRows = await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join assignment in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on status.ExamAssignmentId equals assignment.Id
                where assignment.CreatedAt >= weekStart
                      && assignment.CreatedAt < tomorrowStart
                select new ExamStatusProjection
                {
                    BatchId = assignment.BatchId,
                    StudentId = status.StudentId,
                    Score = status.Score
                })
                .ToListAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = batchId.HasValue ? "تفاصيل صحة دفعة محددة" : "تفاصيل صحة جميع الدفعات",
                Subtitle = "صحة الدفعة محسوبة من حضور اليوم + تسليم الواجبات + متوسط الاختبارات.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد بيانات دفعات."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "batch", Header = "الدفعة" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "students", Header = "عدد الطلاب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "attendance", Header = "حضور اليوم" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "homework", Header = "تسليم الواجبات" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "exam", Header = "متوسط الاختبارات" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "health", Header = "HealthScore" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "decision", Header = "الإجراء المقترح" });

            foreach (BatchProjection batch in batchesQuery)
            {
                if (batchId.HasValue && batch.BatchId != batchId.Value)
                {
                    continue;
                }

                List<StudentBatchProjection> batchStudents = studentBatches
                    .Where(item => item.BatchId == batch.BatchId && item.IsActive)
                    .ToList();

                int studentsCount = batchStudents.Select(item => item.StudentId).Distinct().Count();

                int presentToday = (
                    from attendance in todayAttendance
                    join studentBatch in batchStudents
                        on attendance.StudentId equals studentBatch.StudentId
                    where attendance.IsPresent
                    select attendance.StudentId)
                    .Distinct()
                    .Count();

                double attendancePercent = CalculatePercent(presentToday, studentsCount);

                List<HomeworkSetStudentProjection> batchHomework = homeworkRows
                    .Where(item => item.BatchId == batch.BatchId)
                    .ToList();

                double homeworkPercent = CalculatePercent(
                    batchHomework.Count(item => item.IsSubmitted),
                    batchHomework.Count);

                List<ExamStatusProjection> batchExams = examRows
                    .Where(item => item.BatchId == batch.BatchId && item.Score.HasValue)
                    .ToList();

                double examAverage = batchExams.Count > 0
                    ? Math.Round(batchExams.Average(item => item.Score.Value), 2)
                    : 0;

                double healthScore = Math.Round(
                    (attendancePercent * 0.30) +
                    (homeworkPercent * 0.30) +
                    (examAverage * 0.40),
                    2);

                string rowCss = "success";
                string decision = "لا يلزم تدخل الآن";

                if (healthScore < 60)
                {
                    rowCss = "danger";
                    decision = "تدخل عاجل: مراجعة الحضور والواجبات";
                }
                else if (healthScore < 75)
                {
                    rowCss = "warning";
                    decision = "متابعة خلال اليوم";
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = rowCss,
                    ActionUrl = "/Admin/Batches/Details/" + batch.BatchId,
                    ActionText = "فتح الدفعة",
                    Cells = new Dictionary<string, string>
                    {
                        { "batch", batch.BatchName },
                        { "students", studentsCount.ToString() },
                        { "attendance", attendancePercent.ToString("0.##") + "%" },
                        { "homework", homeworkPercent.ToString("0.##") + "%" },
                        { "exam", examAverage.ToString("0.##") + "%" },
                        { "health", healthScore.ToString("0.##") + "%" },
                        { "decision", decision }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "دفعات معروضة",
                Value = model.Rows.Count.ToString(),
                Hint = batchId.HasValue ? "دفعة واحدة" : "كل الدفعات",
                CssClass = "info"
            });

            return model;
        }

        public async Task<AdminDrillDownPageViewModel> GetInstructorActivityAsync(int? instructorId)
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);
            DateTime weekStart = todayStart.AddDays(-6);

            List<InstructorProjection> instructors = await _context.Instructors
                .AsNoTracking()
                .Select(instructor => new InstructorProjection
                {
                    InstructorId = instructor.Id,
                    InstructorName = instructor.FullName
                })
                .OrderBy(instructor => instructor.InstructorName)
                .ToListAsync();

            List<LectureDetailsProjection> lectures = await (
                from lecture in _context.Lecture.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on lecture.BatchId equals batch.Id
                where lecture.Date >= weekStart
                      && lecture.Date < tomorrowStart
                select new LectureDetailsProjection
                {
                    LectureId = lecture.Id,
                    Title = lecture.Title,
                    InstructorId = lecture.InstructorId,
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    StartAt = lecture.Date,
                    EndAt = lecture.Date.AddMinutes(DefaultLectureDurationMinutes)
                })
                .ToListAsync();

            List<ExamProjection> exams = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(item => item.CreatedAt >= weekStart && item.CreatedAt < tomorrowStart)
                .Select(item => new ExamProjection
                {
                    ExamAssignmentId = item.Id,
                    CreatedByInstructorId = item.CreatedByInstructorId
                })
                .ToListAsync();

            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = instructorId.HasValue ? "تفاصيل نشاط مدرب محدد" : "تفاصيل نشاط المدربين",
                Subtitle = "يعرض نشاط المدرب خلال الأسبوع من المحاضرات والاختبارات، ويترك الواجبات العامة حسب AssignedByUserId لأنها غير مربوطة مباشرة بـ InstructorId.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = now,
                EmptyMessage = "لا توجد بيانات مدربين."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "instructor", Header = "المدرب" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "lectures", Header = "محاضرات الأسبوع" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "running", Header = "جارية اليوم" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "exams", Header = "اختبارات الأسبوع" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "activity", Header = "ActivityScore" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "decision", Header = "الإجراء المقترح" });

            foreach (InstructorProjection instructor in instructors)
            {
                if (instructorId.HasValue && instructor.InstructorId != instructorId.Value)
                {
                    continue;
                }

                List<LectureDetailsProjection> instructorLectures = lectures
                    .Where(item => item.InstructorId == instructor.InstructorId)
                    .ToList();

                int lecturesCount = instructorLectures.Count;

                int runningToday = instructorLectures.Count(item =>
                    item.StartAt <= now &&
                    item.EndAt >= now);

                int examsCount = exams.Count(item =>
                    item.CreatedByInstructorId.HasValue &&
                    item.CreatedByInstructorId.Value == instructor.InstructorId);

                double activityScore = Math.Min(100, (lecturesCount * 12) + (examsCount * 18));

                string rowCss = "success";
                string decision = "نشاط مستقر";

                if (activityScore < 55)
                {
                    rowCss = "danger";
                    decision = "مراجعة خطة المدرب";
                }
                else if (activityScore < 75)
                {
                    rowCss = "warning";
                    decision = "متابعة النشاط خلال الأسبوع";
                }

                model.Rows.Add(new AdminDrillDownRowViewModel
                {
                    RowCssClass = rowCss,
                    ActionUrl = "/Admin/Instructors/Details/" + instructor.InstructorId,
                    ActionText = "فتح المدرب",
                    Cells = new Dictionary<string, string>
                    {
                        { "instructor", instructor.InstructorName },
                        { "lectures", lecturesCount.ToString() },
                        { "running", runningToday.ToString() },
                        { "exams", examsCount.ToString() },
                        { "activity", activityScore.ToString("0.##") + "%" },
                        { "decision", decision }
                    }
                });
            }

            model.Kpis.Add(new AdminDrillDownKpiViewModel
            {
                Label = "مدربين معروضين",
                Value = model.Rows.Count.ToString(),
                Hint = instructorId.HasValue ? "مدرب واحد" : "كل المدربين",
                CssClass = "info"
            });

            return model;
        }

        public Task<AdminDrillDownPageViewModel> GetSmartAlertsReferenceAsync()
        {
            AdminDrillDownPageViewModel model = new AdminDrillDownPageViewModel
            {
                Title = "مرجعية التنبيهات الذكية",
                Subtitle = "هذه الصفحة توضح معنى كل تنبيه ومن أين يأتي قراره.",
                BackUrl = "/Admin/AdminOperationsDashboard",
                GeneratedAt = DateTime.Now,
                EmptyMessage = "لا توجد تنبيهات معرفة."
            };

            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "alert", Header = "التنبيه" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "source", Header = "مصدره" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "condition", Header = "شرط الظهور" });
            model.Columns.Add(new AdminDrillDownColumnViewModel { Key = "action", Header = "الإجراء" });

            model.Rows.Add(new AdminDrillDownRowViewModel
            {
                RowCssClass = "danger",
                Cells = new Dictionary<string, string>
                {
                    { "alert", "محاضرات بحضور منخفض" },
                    { "source", "AttendanceRecords + Lecture" },
                    { "condition", "نسبة الحضور أقل من 70%" },
                    { "action", "مراجعة الحضور أو تنبيه المدرب" }
                }
            });

            model.Rows.Add(new AdminDrillDownRowViewModel
            {
                RowCssClass = "warning",
                Cells = new Dictionary<string, string>
                {
                    { "alert", "واجبات تغلق قريبًا" },
                    { "source", "HomeworkSets.EndAt" },
                    { "condition", "يغلق خلال 24 ساعة" },
                    { "action", "إرسال تذكير للدفعة" }
                }
            });

            model.Rows.Add(new AdminDrillDownRowViewModel
            {
                RowCssClass = "warning",
                Cells = new Dictionary<string, string>
                {
                    { "alert", "دفعة تحتاج تدخل" },
                    { "source", "الحضور + الواجبات + الاختبارات" },
                    { "condition", "HealthScore أقل من 60%" },
                    { "action", "فتح تقرير الدفعة" }
                }
            });

            return Task.FromResult(model);
        }

        private async Task<List<StudentBatchProjection>> LoadStudentBatchesAsync()
        {
            return await (
                from enrollment in _context.StudentBatchEnrollments.AsNoTracking()
                join student in _context.Students.AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                select new StudentBatchProjection
                {
                    StudentId = student.StudentID,
                    BatchId = enrollment.BatchId,
                    IsActive =
                        (student.EnrollmentStatus == "نشط" || student.EnrollmentStatus == "Active")
                        && (enrollment.Status == "Active" || enrollment.Status == "نشط")
                })
                .ToListAsync();
        }

        private double CalculatePercent(int value, int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            return Math.Round((value / (double)total) * 100, 2);
        }

        private class ActiveStudentProjection
        {
            public int StudentId { get; set; }

            public string FullName { get; set; } = string.Empty;

            public string NationalId { get; set; } = string.Empty;

            public string? PhoneNumber { get; set; }

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public DateTime? LastLoginAt { get; set; }

            public string? UserId { get; set; }
        }

        private class StudentBatchProjection
        {
            public int StudentId { get; set; }

            public int BatchId { get; set; }

            public bool IsActive { get; set; }
        }

        private class AttendanceProjection
        {
            public int LectureId { get; set; }

            public int StudentId { get; set; }

            public bool IsPresent { get; set; }
        }

        private class LectureDetailsProjection
        {
            public int LectureId { get; set; }

            public string Title { get; set; } = string.Empty;

            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;

            public int InstructorId { get; set; }

            public string InstructorName { get; set; } = string.Empty;

            public string SectionTitle { get; set; } = string.Empty;

            public DateTime StartAt { get; set; }

            public DateTime EndAt { get; set; }

            public string? Location { get; set; }
        }

        private class ExamProjection
        {
            public int ExamAssignmentId { get; set; }

            public string Title { get; set; } = string.Empty;

            public string BatchName { get; set; } = string.Empty;

            public DateTime ScheduledDate { get; set; }

            public int DurationMinutes { get; set; }

            public int TotalQuestions { get; set; }

            public bool IsOnline { get; set; }

            public bool IsInLab { get; set; }

            public int? CreatedByInstructorId { get; set; }
        }

        private class AttendanceDetailsProjection
        {
            public int StudentId { get; set; }

            public string StudentName { get; set; } = string.Empty;

            public string BatchName { get; set; } = string.Empty;

            public int LectureId { get; set; }

            public string LectureTitle { get; set; } = string.Empty;

            public bool IsPresent { get; set; }

            public DateTime RecordedAt { get; set; }

            public string? Notes { get; set; }
        }

        private class HomeworkProjection
        {
            public int HomeworkSetId { get; set; }

            public string Title { get; set; } = string.Empty;

            public string BatchName { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }

            public DateTime EndAt { get; set; }

            public bool IsSent { get; set; }
        }

        private class BatchProjection
        {
            public int BatchId { get; set; }

            public string BatchName { get; set; } = string.Empty;
        }

        private class HomeworkSetStudentProjection
        {
            public int BatchId { get; set; }

            public int StudentId { get; set; }

            public bool IsSubmitted { get; set; }
        }

        private class ExamStatusProjection
        {
            public int BatchId { get; set; }

            public int StudentId { get; set; }

            public int? Score { get; set; }
        }

        private class InstructorProjection
        {
            public int InstructorId { get; set; }

            public string InstructorName { get; set; } = string.Empty;
        }
    }
}
