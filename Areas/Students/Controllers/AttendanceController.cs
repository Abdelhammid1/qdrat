using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Implementations;
using QdratNew.ViewModels.Students;
using System.Globalization;
using System.Security.Claims;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StudentAnalyticsHelper _analyticsHelper;

        public AttendanceController(ApplicationDbContext context, StudentAnalyticsHelper analyticsHelper)
        {
            _context = context;
            _analyticsHelper = analyticsHelper;
        }

        public async Task<IActionResult> Index(int? batchId, int? courseId, string? month)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => new { s.StudentID, s.FullName })
                .FirstOrDefaultAsync();

            if (student == null) return NotFound();

            // ── جلب كل سجلات الحضور بتفاصيلها ─────────────────────────────
            var rawRecords = await (
                from a in _context.AttendanceRecords.AsNoTracking()
                where a.StudentId == student.StudentID
                join lec in _context.Lecture.AsNoTracking() on a.LectureId equals lec.Id
                join sec in _context.Sections.AsNoTracking() on lec.SectionId equals sec.Id into jsec
                from sec in jsec.DefaultIfEmpty()
                join crs in _context.Courses.AsNoTracking() on lec.CourseId equals crs.Id into jcrs
                from crs in jcrs.DefaultIfEmpty()
                join bat in _context.Batches.AsNoTracking() on lec.BatchId equals bat.Id into jbat
                from bat in jbat.DefaultIfEmpty()
                join ins in _context.Instructors.AsNoTracking() on lec.InstructorId equals ins.Id into jins
                from ins in jins.DefaultIfEmpty()
                orderby lec.Date descending
                select new AttendanceRecordItemVM
                {
                    LectureId      = lec.Id,
                    LectureTitle   = lec.Title,
                    Date           = lec.Date,
                    Course         = crs != null ? crs.Name : null,
                    CourseId       = crs != null ? (int?)crs.Id : null,
                    Section        = sec != null ? sec.Title : null,
                    BatchName      = bat != null ? bat.Name : null,
                    BatchId        = bat != null ? (int?)bat.Id : null,
                    InstructorName = ins != null ? ins.FullName : null,
                    IsPresent      = a.IsPresent,
                    IsLateArrival  = a.IsLateArrival,
                    HasEarlyLeavePermission = a.HasEarlyLeavePermission,
                    Notes          = a.Notes,
                    ActualArrivalTime = a.ActualArrivalTime,
                    ScheduledTime  = lec.ScheduledTime,
                }
            ).ToListAsync();

            // ── دُفعات الطالب ─────────────────────────────────────────────
            var batches = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on e.BatchId equals b.Id
                where e.StudentID == student.StudentID
                orderby b.Name
                select new BatchMiniVM { Id = b.Id, Name = b.Name }
            ).ToListAsync();

            // ── تطبيق الفلاتر ─────────────────────────────────────────────
            var filtered = rawRecords.AsEnumerable();

            if (batchId.HasValue)
                filtered = filtered.Where(r => r.BatchId == batchId.Value);

            if (courseId.HasValue)
                filtered = filtered.Where(r => r.CourseId == courseId.Value);

            if (!string.IsNullOrEmpty(month) && month.Length == 7)
                filtered = filtered.Where(r => r.MonthKey == month);

            var records = filtered.ToList();

            // ── ملخص حسب الدورة ──────────────────────────────────────────
            var courseSummaries = rawRecords
                .Where(r => r.CourseId.HasValue)
                .GroupBy(r => new { r.CourseId, r.Course })
                .Select(g => new CourseAttendanceSummaryVM
                {
                    CourseId   = g.Key.CourseId!.Value,
                    CourseName = g.Key.Course ?? "بدون دورة",
                    Total      = g.Count(),
                    Attended   = g.Count(r => r.IsPresent),
                    Absent     = g.Count(r => !r.IsPresent),
                    Late       = g.Count(r => r.IsPresent && r.IsLateArrival),
                })
                .OrderByDescending(c => c.Total)
                .ToList();

            // ── ملخص شهري ────────────────────────────────────────────────
            var arabicMonths = new[]
            {
                "يناير","فبراير","مارس","أبريل","مايو","يونيو",
                "يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر"
            };

            var monthlyStats = rawRecords
                .GroupBy(r => new { r.Date.Year, r.Date.Month })
                .Select(g => new MonthlyAttendanceVM
                {
                    Year       = g.Key.Year,
                    Month      = g.Key.Month,
                    MonthLabel = $"{arabicMonths[g.Key.Month - 1]} {g.Key.Year}",
                    Total      = g.Count(),
                    Attended   = g.Count(r => r.IsPresent),
                    Absent     = g.Count(r => !r.IsPresent),
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            // ── حساب السلاسل ─────────────────────────────────────────────
            var (currentStreak, bestStreak) = CalculateStreaks(rawRecords);

            // ── توصيات الذكاء الاصطناعي ──────────────────────────────────
            int total    = rawRecords.Count;
            int attended = rawRecords.Count(r => r.IsPresent);
            int absent   = total - attended;
            int late     = rawRecords.Count(r => r.IsPresent && r.IsLateArrival);
            double rate  = total == 0 ? 0 : Math.Round((double)attended / total * 100, 1);

            var aiRecs = GenerateAIRecommendations(total, attended, absent, late, rate,
                currentStreak, bestStreak, courseSummaries, monthlyStats);

            // ── بناء الـ ViewModel النهائي ────────────────────────────────
            var vm = new StudentAttendanceDashboardVM
            {
                StudentId      = student.StudentID,
                StudentName    = student.FullName,
                BatchNames     = string.Join("، ", batches.Select(b => b.Name)),
                TotalLectures  = total,
                AttendedCount  = attended,
                AbsentCount    = absent,
                ExcusedCount   = rawRecords.Count(r => !r.IsPresent && !string.IsNullOrWhiteSpace(r.Notes)),
                LateCount      = late,
                CurrentStreak  = currentStreak,
                BestStreak     = bestStreak,
                Records        = records,
                CourseSummaries = courseSummaries,
                MonthlyStats   = monthlyStats,
                Batches        = batches,
                AIRecommendations = aiRecs,
                FilterBatchId  = batchId,
                FilterCourseId = courseId,
                FilterMonth    = month,
            };

            // ── تسجيل النشاط التحليلي ─────────────────────────────────────
            var last = rawRecords.FirstOrDefault();
            if (last != null)
            {
                try
                {
                    await _analyticsHelper.SafeRecordAttendanceAsync(
                        student.StudentID, last.LectureId, last.IsPresent);
                }
                catch { /* لا نوقف الصفحة بسبب خطأ تحليلي */ }
            }

            return View(vm);
        }

        // ── حساب السلاسل ──────────────────────────────────────────────────
        private static (int current, int best) CalculateStreaks(List<AttendanceRecordItemVM> records)
        {
            if (!records.Any()) return (0, 0);

            var sorted = records.OrderByDescending(r => r.Date).ToList();
            int current = 0;
            int best = 0;
            int temp = 0;

            foreach (var r in sorted)
            {
                if (r.IsPresent) temp++;
                else temp = 0;
                if (temp > best) best = temp;
            }

            foreach (var r in sorted)
            {
                if (r.IsPresent) current++;
                else break;
            }

            return (current, best);
        }

        // ── توليد توصيات الذكاء الاصطناعي ────────────────────────────────
        private static List<AIAttendanceRecommendationVM> GenerateAIRecommendations(
            int total, int attended, int absent, int late, double rate,
            int currentStreak, int bestStreak,
            List<CourseAttendanceSummaryVM> courses,
            List<MonthlyAttendanceVM> monthly)
        {
            var recs = new List<AIAttendanceRecommendationVM>();

            if (total == 0)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "لا توجد بيانات حضور",
                    Message = "لم يُسجَّل لك أي حضور بعد. تأكد من تسجيل حضورك في المحاضرات القادمة.",
                    Type    = "info",
                    Icon    = "bi-info-circle",
                    Priority = 1
                });
                return recs;
            }

            // ── تحليل المعدل العام ────────────────────────────────────────
            if (rate >= 90)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "أداء رائع! استمر",
                    Message = $"نسبة حضورك {rate}% تعكس التزامك الممتاز. أنت من أفضل الطلاب انتظاماً — واصل هذا المستوى للحفاظ على تفوقك الأكاديمي.",
                    Type    = "success",
                    Icon    = "bi-trophy-fill",
                    Priority = 1
                });
            }
            else if (rate >= 80)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "مستوى جيد — يمكنك الأفضل",
                    Message = $"نسبة حضورك {rate}%. أنت قريب من الممتاز! تحضير {Math.Ceiling((90 - rate) / 100 * total)} محاضرة إضافية سيرفعك إلى مستوى 90%.",
                    Type    = "info",
                    Icon    = "bi-graph-up-arrow",
                    Priority = 1
                });
            }
            else if (rate >= 70)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "تحذير: نسبة الحضور منخفضة",
                    Message = $"نسبة حضورك {rate}% أقل من الحد المطلوب (80%). غيابك عن {absent} محاضرة يؤثر على فهمك للمادة وقد يعرضك لعقوبات أكاديمية.",
                    Type    = "warning",
                    Icon    = "bi-exclamation-triangle-fill",
                    Priority = 1
                });
            }
            else
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "تنبيه عاجل: مستوى حرج",
                    Message = $"نسبة حضورك {rate}% في المنطقة الحرجة. الغياب عن {absent} محاضرة من أصل {total} يضع مشاركتك في البرنامج في خطر. يُنصح بمراجعة الإدارة فوراً.",
                    Type    = "danger",
                    Icon    = "bi-shield-x",
                    Priority = 1
                });
            }

            // ── تحليل التأخر ──────────────────────────────────────────────
            if (late > 0 && attended > 0)
            {
                double lateRate = Math.Round((double)late / attended * 100, 1);
                if (lateRate >= 30)
                {
                    recs.Add(new AIAttendanceRecommendationVM
                    {
                        Title   = "نمط تأخر متكرر",
                        Message = $"لاحظ النظام أنك تأخرت في {late} من أصل {attended} محاضرة حضرتها ({lateRate}%). التأخر المتكرر يؤثر على استيعاب المقدمات والمعلومات الأساسية. حاول المغادرة مبكراً بـ 15 دقيقة.",
                        Type    = "warning",
                        Icon    = "bi-clock-history",
                        Priority = 2
                    });
                }
            }

            // ── تحليل السلسلة ─────────────────────────────────────────────
            if (currentStreak >= 5)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = $"سلسلة حضور مستمرة: {currentStreak} محاضرات",
                    Message = $"أحسنت! لقد حضرت {currentStreak} محاضرات متتالية. استمر في هذا الإيقاع وستحقق أفضل سجل حضور لك (سجلك الأعلى: {bestStreak}).",
                    Type    = "success",
                    Icon    = "bi-fire",
                    Priority = 2
                });
            }
            else if (currentStreak == 0 && absent > 0)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "ابدأ سلسلة حضور جديدة",
                    Message = $"آخر سجل لك كان غياباً. الآن هو الوقت المثالي لبدء سلسلة حضور منتظمة. سجلك الأعلى هو {bestStreak} محاضرات متتالية — يمكنك تجاوزه!",
                    Type    = "info",
                    Icon    = "bi-lightning-charge-fill",
                    Priority = 3
                });
            }

            // ── تحليل الدورات الأضعف ──────────────────────────────────────
            var weakCourses = courses.Where(c => c.Rate < 70 && c.Total >= 3).ToList();
            if (weakCourses.Any())
            {
                var names = string.Join("، ", weakCourses.Select(c => $"{c.CourseName} ({c.Rate}%)"));
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "دورات تحتاج انتباهاً",
                    Message = $"نسبة حضورك منخفضة في: {names}. ركّز على هذه الدورات وتواصل مع المدرب لتعويض ما فاتك.",
                    Type    = "warning",
                    Icon    = "bi-book-half",
                    Priority = 2
                });
            }

            // ── تحليل الاتجاه الشهري ─────────────────────────────────────
            if (monthly.Count >= 2)
            {
                var last2 = monthly.TakeLast(2).ToList();
                double prevRate = last2[0].Rate;
                double currRate = last2[1].Rate;
                double diff = Math.Round(currRate - prevRate, 1);

                if (diff >= 10)
                {
                    recs.Add(new AIAttendanceRecommendationVM
                    {
                        Title   = "تحسن ملحوظ هذا الشهر",
                        Message = $"ارتفعت نسبة حضورك بمقدار {diff}% مقارنة بالشهر الماضي ({prevRate}% → {currRate}%). استمر في هذا التحسن!",
                        Type    = "success",
                        Icon    = "bi-arrow-up-circle-fill",
                        Priority = 3
                    });
                }
                else if (diff <= -10)
                {
                    recs.Add(new AIAttendanceRecommendationVM
                    {
                        Title   = "تراجع في الحضور هذا الشهر",
                        Message = $"انخفضت نسبة حضورك بمقدار {Math.Abs(diff)}% عن الشهر الماضي ({prevRate}% → {currRate}%). راجع جدولك وتأكد من عدم وجود تعارضات.",
                        Type    = "warning",
                        Icon    = "bi-arrow-down-circle-fill",
                        Priority = 2
                    });
                }
            }

            // ── نصيحة الغياب بعذر ────────────────────────────────────────
            var excused = attended == 0 ? 0 : (int)Math.Round((double)(total - attended) / total * 100);
            if (absent >= 3)
            {
                recs.Add(new AIAttendanceRecommendationVM
                {
                    Title   = "نصيحة: وثّق غيابك",
                    Message = "في حال وجود أعذار للغياب، يُنصح بتوثيقها فورياً مع المدرب أو الإدارة لتجنب احتساب الغياب غير المبرر الذي يؤثر على تقييمك.",
                    Type    = "info",
                    Icon    = "bi-file-earmark-text",
                    Priority = 4
                });
            }

            return recs.OrderBy(r => r.Priority).ToList();
        }
    }
}
