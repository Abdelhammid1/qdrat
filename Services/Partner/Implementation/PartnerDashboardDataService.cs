using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.ViewModels.Partner.Dashboard;

namespace QdratNew.Services.Partner.Implementation
{
    public class PartnerDashboardDataService : IPartnerDashboardDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan StudentCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan HomeworkCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ExamCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ContractCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ChartsCacheDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ListsCacheDuration = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan DashboardCacheDuration =
            TimeSpan.FromMinutes(5);

        public PartnerDashboardDataService(
            ApplicationDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public PartnerDashboardViewModel GetDashboard(int partnerId)
        {
            return new PartnerDashboardViewModel
            {
                StudentKpis = GetStudentKpisCached(partnerId),
                HomeworkKpis = GetHomeworkKpisCached(partnerId),
                ExamKpis = GetExamKpisCached(partnerId),
                ContractKpis = GetContractKpisCached(partnerId),

                Charts = GetChartsCached(partnerId),

                Batches = GetBatchesCached(partnerId),
                Instructors = GetInstructorsCached(partnerId),

                DecisionAlerts = GetDecisionAlertsCached(partnerId)
            };
        }

        // =====================================================
        // 🔽 كل جزء معزول – Read Only
        // =====================================================

        private PartnerStudentKpiViewModel GetStudentKpis(int partnerId)
        {
            // =====================================================
            // 1️⃣ جلب كل الطلاب المرتبطين بالشريك (Query واحدة)
            // =====================================================
            var students =
                (
                    from s in _context.Students
                    join e in _context.StudentBatchEnrollments
                        on s.StudentID equals e.StudentID
                    join b in _context.Batches
                        on e.BatchId equals b.Id
                    join br in _context.Branches
                        on b.BranchId equals br.Id
                    where
                        br.PartnerId == partnerId &&
                        !b.IsDeleted &&
                        b.IsActive
                    select new
                    {
                        StudentId = s.StudentID,
                        s.IsActiveForLearning,
                        StudentEnrollmentStatus = s.EnrollmentStatus,
                        BatchEnrollmentStatus = e.Status
                    }
                )
                .AsNoTracking()
                .ToList();

            // =====================================================
            // 2️⃣ الحساب في الذاكرة (مرة واحدة)
            // =====================================================

            var totalStudents = students
                .Select(x => x.StudentId)
                .Distinct()
                .Count();

            var activeStudents = students
                .Where(x =>
                    x.IsActiveForLearning &&
                    x.StudentEnrollmentStatus == "نشط")
                .Select(x => x.StudentId)
                .Distinct()
                .Count();

            var atRiskStudents = students
                .Where(x =>
                    !x.IsActiveForLearning ||
                    x.StudentEnrollmentStatus != "نشط")
                .Select(x => x.StudentId)
                .Distinct()
                .Count();

            var startedStudentIds = students
                .Where(x => x.BatchEnrollmentStatus == "Active")
                .Select(x => x.StudentId)
                .Distinct()
                .ToHashSet();

            var notStartedStudents =
                totalStudents - startedStudentIds.Count;

            var activePercentage =
                totalStudents == 0
                    ? 0
                    : Math.Round(
                        (decimal)activeStudents / totalStudents * 100,
                        2);

            // =====================================================
            // 3️⃣ النتيجة النهائية
            // =====================================================
            return new PartnerStudentKpiViewModel
            {
                TotalStudents = totalStudents,
                ActiveStudents = activeStudents,
                AtRiskStudents = atRiskStudents,
                NotStartedStudents = notStartedStudents,
                ActivePercentage = activePercentage
            };
        }



        private List<ChartSegmentViewModel> GetStudentLevelDistribution(int partnerId)
        {
            // =====================================================
            // 1️⃣ Query واحدة لجلب مستويات الطلاب المرتبطين بالشريك
            // =====================================================

            var rawData = (
                from s in _context.Students
                join be in _context.StudentBatchEnrollments
                    on s.StudentID equals be.StudentID
                join b in _context.Batches
                    on be.BatchId equals b.Id
                join br in _context.Branches
                    on b.BranchId equals br.Id
                where
                    br.PartnerId == partnerId &&
                    b.IsActive &&
                    !b.IsDeleted &&
                    s.Level != null
                select new
                {
                    s.StudentID,
                    Level = s.Level
                })
                .AsNoTracking()
                .ToList();

            if (!rawData.Any())
                return new List<ChartSegmentViewModel>();

            // =====================================================
            // 2️⃣ إزالة التكرار (طالب قد يكون في أكثر من دفعة)
            // =====================================================

            var distinctStudents = rawData
                .GroupBy(x => x.StudentID)
                .Select(g => g.First())
                .ToList();

            // =====================================================
            // 3️⃣ التجميع في الذاكرة (مرة واحدة)
            // =====================================================

            var result = distinctStudents
                .GroupBy(x => x.Level)
                .Select(g => new ChartSegmentViewModel
                {
                    Segment = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return result;
        }


        private List<StackedBarViewModel> GetHomeworkCommitmentChart(int partnerId)
        {
            // =====================================================
            // 1️⃣ Query واحدة لجلب حالات الواجبات لكل دفعة
            // =====================================================

            var rawData = (
                from hss in _context.HomeworkSetStudents
                join hs in _context.HomeworkSets on hss.HomeworkSetId equals hs.Id
                join b in _context.Batches on hs.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                where
                    br.PartnerId == partnerId &&
                    hs.IsSent &&
                    b.IsActive &&
                    !b.IsDeleted
                select new
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    hss.IsSubmitted,
                    hss.AssignedAt,
                    hss.SubmittedAt
                })
                .AsNoTracking()
                .ToList();

            if (!rawData.Any())
                return new List<StackedBarViewModel>();

            // =====================================================
            // 2️⃣ التجميع في الذاكرة (مرة واحدة)
            // =====================================================

            var result = rawData
                .GroupBy(x => new { x.BatchId, x.BatchName })
                .Select(g =>
                {
                    var completed = g.Count(x => x.IsSubmitted);

                    var late = g.Count(x =>
                        !x.IsSubmitted &&
                        x.SubmittedAt == null &&
                        x.AssignedAt < DateTime.Now);

                    var notStarted = g.Count(x =>
                        !x.IsSubmitted &&
                        x.SubmittedAt == null &&
                        x.AssignedAt == default);

                    return new StackedBarViewModel
                    {
                        Name = g.Key.BatchName,
                        Completed = completed,
                        Late = late,
                        NotStarted = notStarted
                    };
                })
                .OrderByDescending(x => x.Late)
                .ToList();

            return result;
        }





        private PartnerStudentKpiViewModel GetStudentKpisCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:StudentKPIs";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = StudentCacheDuration;
                return GetStudentKpis(partnerId);
            });
        }


        private PartnerHomeworkKpiViewModel GetHomeworkKpisCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:HomeworkKPIs";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = HomeworkCacheDuration;
                return GetHomeworkKpis(partnerId);
            });
        }


        private PartnerExamKpiViewModel GetExamKpisCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:ExamKPIs";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ExamCacheDuration;
                return GetExamKpis(partnerId);
            });
        }


        private PartnerContractKpiViewModel GetContractKpisCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:ContractKPIs";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ContractCacheDuration;
                return GetContractKpis(partnerId);
            });
        }


        private PartnerChartsViewModel GetChartsCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:Charts";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ChartsCacheDuration;
                return GetCharts(partnerId);
            });
        }


        private List<PartnerBatchSummaryViewModel> GetBatchesCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:Batches";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ListsCacheDuration;
                return GetBatches(partnerId);
            });
        }

        private List<PartnerInstructorSummaryViewModel> GetInstructorsCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:Instructors";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ListsCacheDuration;
                return GetInstructors(partnerId);
            });
        }


        private List<PartnerDecisionAlertViewModel> GetDecisionAlertsCached(int partnerId)
        {
            var cacheKey = $"Partner:{partnerId}:DecisionAlerts";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
                return GetDecisionAlerts(partnerId);
            });
        }

        private PartnerExamKpiViewModel GetExamKpis(int partnerId)
        {
            // =====================================================
            // 1️⃣ جلب كل حالات الاختبارات المرتبطة بالشريك (Query واحدة)
            // =====================================================

            var examData = (
                from es in _context.ExamStudentStatuses
                join s in _context.Students on es.StudentId equals s.StudentID
                join e in _context.Exams on es.ExamId equals e.Id
                join be in _context.StudentBatchEnrollments on s.StudentID equals be.StudentID
                join b in _context.Batches on be.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                where
                    br.PartnerId == partnerId &&
                    b.IsActive &&
                    !b.IsDeleted
                select new
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    es.IsSubmitted,
                    es.Score,
                    es.EndAt
                })
                .AsNoTracking()
                .ToList();

            if (!examData.Any())
            {
                return new PartnerExamKpiViewModel();
            }

            // =====================================================
            // 2️⃣ الحسابات في الذاكرة (مرة واحدة)
            // =====================================================

            var completedExams =
                examData.Count(x => x.IsSubmitted);

            var lateExams =
                examData.Count(x =>
                    !x.IsSubmitted &&
                    x.EndAt.HasValue &&
                    x.EndAt.Value < DateTime.Now);

            var gradedExams =
                examData.Where(x => x.Score.HasValue).ToList();

            decimal averageSuccessRate =
       gradedExams.Any()
           ? Math.Round(
               (decimal)gradedExams.Average(x => x.Score.Value),
               2)
           : 0m;


            // =====================================================
            // 3️⃣ أقل دفعة أداءً
            // =====================================================

            var lowestBatchPerformance = examData
                .Where(x => x.Score.HasValue)
                .GroupBy(x => new { x.BatchId, x.BatchName })
                .Select(g => new
                {
                    AverageScore = g.Average(x => x.Score.Value)
                })
                .OrderBy(x => x.AverageScore)
                .Select(x => x.AverageScore)
                .FirstOrDefault();

            // =====================================================
            // 4️⃣ النتيجة النهائية
            // =====================================================

            return new PartnerExamKpiViewModel
            {
                CompletedExams = completedExams,
                LateExams = lateExams,
                AverageSuccessRate = averageSuccessRate,
                LowestBatchPerformance = Math.Round((decimal)lowestBatchPerformance, 2)
            };
        }


        private PartnerHomeworkKpiViewModel GetHomeworkKpis(int partnerId)
        {
            // =====================================================
            // 1️⃣ جلب كل بيانات الواجبات المرتبطة بالشريك (Query واحدة)
            // =====================================================

            var homeworkData = (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                join hss in _context.HomeworkSetStudents on hs.Id equals hss.HomeworkSetId
                where
                    br.PartnerId == partnerId &&
                    hs.IsSent &&
                    b.IsActive &&
                    !b.IsDeleted
                select new
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    hss.IsSubmitted
                })
                .AsNoTracking()
                .ToList();

            if (!homeworkData.Any())
            {
                return new PartnerHomeworkKpiViewModel();
            }

            // =====================================================
            // 2️⃣ الحسابات في الذاكرة (مرة واحدة)
            // =====================================================

            var sentHomeworks =
                homeworkData
                    .Select(x => x.BatchId)
                    .Distinct()
                    .Count();

            var totalAssignments = homeworkData.Count;

            var submittedAssignments =
                homeworkData.Count(x => x.IsSubmitted);

            var unsolvedHomeworks =
                totalAssignments - submittedAssignments;

            decimal averageCommitmentRate =
                totalAssignments == 0
                    ? 0
                    : Math.Round(
                        (decimal)submittedAssignments / totalAssignments * 100,
                        2);

            // =====================================================
            // 3️⃣ أكثر دفعة إهمالًا
            // =====================================================

            var mostNeglectedBatch = homeworkData
                .GroupBy(x => new { x.BatchId, x.BatchName })
                .Select(g => new
                {
                    g.Key.BatchName,
                    CommitmentRate =
                        g.Count() == 0
                            ? 0
                            : (decimal)g.Count(x => x.IsSubmitted) / g.Count()
                })
                .OrderBy(x => x.CommitmentRate)
                .FirstOrDefault();

            // =====================================================
            // 4️⃣ النتيجة النهائية
            // =====================================================

            return new PartnerHomeworkKpiViewModel
            {
                SentHomeworks = sentHomeworks,
                UnsolvedHomeworks = unsolvedHomeworks,
                AverageCommitmentRate = averageCommitmentRate,
                MostNeglectedBatchName = mostNeglectedBatch?.BatchName
            };
        }


        private PartnerInstructorKpiViewModel GetInstructorKpis(int partnerId)
        {
            return new PartnerInstructorKpiViewModel();
        }

        private PartnerContractKpiViewModel GetContractKpis(int partnerId)
        {
            // =====================================================
            // 1️⃣ جلب الفترة النشطة الحالية للشريك
            // =====================================================

            var today = DateTime.Today;

            var activePeriod = _context.PartnerSubscriptionPeriods
                .AsNoTracking()
                .Where(p =>
                    p.PartnerSubscription.PartnerId == partnerId &&
                    p.StartDate <= today &&
                    p.EndDate >= today)
                .Select(p => new
                {
                    p.StartDate,
                    p.EndDate,
                    p.MaxStudents,
                    UsedStudents = p.Students.Count
                })
                .FirstOrDefault();

            // =====================================================
            // 2️⃣ في حالة عدم وجود فترة نشطة (أمان)
            // =====================================================

            if (activePeriod == null)
            {
                return new PartnerContractKpiViewModel
                {
                    ContractStatus = "غير نشط",
                    RemainingDays = 0
                };
            }

            // =====================================================
            // 3️⃣ الحسابات (خفيفة وثابتة)
            // =====================================================

            var totalDays =
                (activePeriod.EndDate.Date - activePeriod.StartDate.Date).Days;

            var elapsedDays =
                (today - activePeriod.StartDate.Date).Days;

            var remainingDays =
                (activePeriod.EndDate.Date - today).Days;

            if (remainingDays < 0)
                remainingDays = 0;

            decimal studentUsagePercentage = 0;
            if (activePeriod.MaxStudents.HasValue && activePeriod.MaxStudents.Value > 0)
            {
                studentUsagePercentage = Math.Round(
                    (decimal)activePeriod.UsedStudents /
                    activePeriod.MaxStudents.Value * 100,
                    2);
            }

            decimal timeUsagePercentage = 0;
            if (totalDays > 0)
            {
                timeUsagePercentage = Math.Round(
                    (decimal)elapsedDays / totalDays * 100,
                    2);
            }

            // =====================================================
            // 4️⃣ الحالة النصية للعقد
            // =====================================================

            string contractStatus;
            if (remainingDays <= 0)
                contractStatus = "منتهي";
            else if (remainingDays <= 30)
                contractStatus = "قارب على الانتهاء";
            else
                contractStatus = "نشط";

            // =====================================================
            // 5️⃣ النتيجة النهائية
            // =====================================================

            return new PartnerContractKpiViewModel
            {
                ContractStatus = contractStatus,
                EndDate = activePeriod.EndDate,
                RemainingDays = remainingDays,

                MaxAllowedStudents = activePeriod.MaxStudents ?? 0,
                UsedStudents = activePeriod.UsedStudents,

                StudentUsagePercentage = studentUsagePercentage,
                TimeUsagePercentage = timeUsagePercentage
            };
        }


        private PartnerChartsViewModel GetCharts(int partnerId)
        {
            return new PartnerChartsViewModel
            {
                StudentPerformanceOverTime =
                    GetStudentPerformanceOverTime(partnerId),

                BatchPerformanceComparison =
                    GetBatchPerformanceComparison(partnerId),

                StudentLevelDistribution =
                    GetStudentLevelDistribution(partnerId),

                HomeworkCommitment =
                    GetHomeworkCommitmentChart(partnerId),

                InstructorActivity = new()
            };
        }


        private List<ChartPointViewModel> GetStudentPerformanceOverTime(int partnerId)
        {
            // =====================================================
            // 1️⃣ جلب بيانات الدرجات مع التاريخ (Query واحدة)
            // =====================================================

            var rawData = (
                from es in _context.ExamStudentStatuses
                join s in _context.Students on es.StudentId equals s.StudentID
                join be in _context.StudentBatchEnrollments on s.StudentID equals be.StudentID
                join b in _context.Batches on be.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                where
                    br.PartnerId == partnerId &&
                    es.Score.HasValue &&
                    es.SubmittedAt.HasValue &&
                    b.IsActive &&
                    !b.IsDeleted
                select new
                {
                    Month = es.SubmittedAt.Value.Month,
                    Year = es.SubmittedAt.Value.Year,
                    Score = es.Score.Value
                })
                .AsNoTracking()
                .ToList();

            if (!rawData.Any())
                return new List<ChartPointViewModel>();

            // =====================================================
            // 2️⃣ التجميع في الذاكرة (مرة واحدة)
            // =====================================================

            var result = rawData
                .GroupBy(x => new { x.Year, x.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new ChartPointViewModel
                {
                    Label = $"{g.Key.Month}/{g.Key.Year}",
                    Value = Math.Round(
                        (decimal)g.Average(x => x.Score),
                        2)
                })
                .ToList();

            return result;
        }

        private List<ChartBarViewModel> GetBatchPerformanceComparison(int partnerId)
        {
            // =====================================================
            // 1️⃣ Query واحدة لجلب درجات الاختبارات مع الدفعات
            // =====================================================

            var rawData = (
                from es in _context.ExamStudentStatuses
                join s in _context.Students on es.StudentId equals s.StudentID
                join be in _context.StudentBatchEnrollments on s.StudentID equals be.StudentID
                join b in _context.Batches on be.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                where
                    br.PartnerId == partnerId &&
                    b.IsActive &&
                    !b.IsDeleted &&
                    es.Score.HasValue
                select new
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    Score = es.Score.Value
                })
                .AsNoTracking()
                .ToList();

            if (!rawData.Any())
                return new List<ChartBarViewModel>();

            // =====================================================
            // 2️⃣ التجميع في الذاكرة (مرة واحدة)
            // =====================================================

            var result = rawData
                .GroupBy(x => new { x.BatchId, x.BatchName })
                .Select(g => new ChartBarViewModel
                {
                    Name = g.Key.BatchName,
                    Value = Math.Round(
                        (decimal)g.Average(x => x.Score),
                        2)
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            return result;
        }



        private List<PartnerBatchSummaryViewModel> GetBatches(int partnerId)
        {
            return new List<PartnerBatchSummaryViewModel>();
        }

        private List<PartnerInstructorSummaryViewModel> GetInstructors(int partnerId)
        {
            return new List<PartnerInstructorSummaryViewModel>();
        }

        private List<PartnerDecisionAlertViewModel> GetDecisionAlerts(int partnerId)
        {
            return new List<PartnerDecisionAlertViewModel>();
        }
    }

}
