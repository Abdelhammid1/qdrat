using QdratNew.Areas.Admin.Controllers;
using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public class MetricDecisionService : IMetricDecisionService
    {
        // ──────────────────────────────────────────────────────────────────────
        public DecisionMetricDetailsVM BuildMetricDecisionDetails(
            string metric,
            AdvancedAnalyticsDashboardVM dashboard)
        {
            metric = string.IsNullOrWhiteSpace(metric)
                ? "total-batches"
                : metric.Trim();

            var totalStudents = dashboard.Students?.Count ?? 0;

            var highPriorityStudents = dashboard.Students?
                .Where(x => x.Priority == "High")
                .OrderBy(x => x.ExamScore)
                .ToList() ?? new List<StudentAnalyticsVM>();

            var highRiskLessons = dashboard.Lessons?
                .Where(x => x.WeakPercentage >= 70)
                .OrderByDescending(x => x.WeakPercentage)
                .ToList() ?? new List<LessonAnalyticsVM>();

            // ── بطاقات KPI مشتركة ───────────────────────────────────────────
            var cards = new List<DecisionMetricCardVM>
            {
                new() { Label = "الدفعات",    Value = dashboard.TotalBatches.ToString(),    Icon = "bi-layers-fill",           Theme = "primary", Hint = "دفعات نشطة" },
                new() { Label = "المدربون",   Value = dashboard.TotalInstructors.ToString(), Icon = "bi-person-badge-fill",     Theme = "info",    Hint = "مدربون مرتبطون بدفعات" },
                new() { Label = "حالات حرجة", Value = dashboard.CriticalCases.ToString(),   Icon = "bi-person-fill-exclamation", Theme = "danger",  Hint = "طلاب أولوية عالية" },
                new() { Label = "دروس خطرة",  Value = dashboard.HighRiskCount.ToString(),   Icon = "bi-exclamation-diamond-fill", Theme = "warning", Hint = "دروس ضعفها 70% أو أكثر" }
            };

            DecisionMetricDetailsVM vm;

            switch (metric)
            {
                // ── إجمالي الدفعات ───────────────────────────────────────────
                case "total-batches":
                    vm = CreateBase(dashboard, metric,
                        "إجمالي الدفعات",
                        "قراءة تنفيذية لحالة الدفعات النشطة داخل نطاق الفلاتر.",
                        "bi-layers-fill", "primary",
                        dashboard.TotalBatches.ToString(), "", cards);

                    vm.Status = dashboard.RiskHigh > 0 ? "تحتاج متابعة" : "مستقرة";
                    vm.StatusClass = dashboard.RiskHigh > 0 ? "warning" : "success";
                    vm.DecisionSummary = "الدفعات هي وحدة القرار الرئيسية: منها يتم تحديد أين يبدأ التدخل ومن المسؤول عنه.";
                    vm.DecisionExplanation = $"يوجد {dashboard.TotalBatches} دفعة نشطة، منها {dashboard.RiskHigh} عالية الخطورة و{dashboard.RiskMedium} متوسطة.";
                    vm.RecommendedAction = dashboard.RiskHigh > 0
                        ? "ابدأ بالدفعات عالية الخطورة وافتح تفاصيل كل دفعة لتحديد الدروس والمدربين المتأثرين."
                        : "استمر في المتابعة الأسبوعية وراقب أي انتقال من مستوى متوسط إلى عالي.";
                    vm.PrimaryActionText = "فتح مخاطر النظام";
                    vm.PrimaryActionName = nameof(AnalyticsDashboardController.SystemRiskDetails);
                    vm.Rows = dashboard.Batches.Select(x => new DecisionMetricRowVM
                    {
                        Name = x.BatchName,
                        Group = x.CourseName,
                        PrimaryValue = $"{Math.Round(x.AvgWeakness, 1)}%",
                        SecondaryValue = $"{x.AffectedStudents} طالب متأثر",
                        RiskLevel = x.RiskLevel,
                        Recommendation = x.AvgWeakness >= 70 ? "تدخل عاجل للدفعة"
                                       : x.AvgWeakness >= 50 ? "خطة متابعة قصيرة"
                                                              : "متابعة دورية",
                        ActionName = nameof(AnalyticsBatchController.BatchPerformanceDetails),
                        BatchId = x.BatchId
                    }).ToList();
                    return vm;

                // ── المدربون ────────────────────────────────────────────────
                case "total-instructors":
                    vm = CreateBase(dashboard, metric,
                        "المدربون النشطون",
                        "تحليل أثر المدربين المرتبطين بالدفعات والدروس الضعيفة.",
                        "bi-person-badge-fill", "info",
                        dashboard.TotalInstructors.ToString(), "", cards);

                    bool hasRiskyInstructor = dashboard.Instructors
                        .Any(x => (x.RiskLevel ?? "").Contains("مرتفع") || (x.RiskLevel ?? "").Contains("عالي"));

                    vm.Status = hasRiskyInstructor ? "تدخل تدريبي" : "مقبول";
                    vm.StatusClass = hasRiskyInstructor ? "danger" : "success";
                    vm.DecisionSummary = "هذا المؤشر يوضح هل الضعف متكرر مع مدرب محدد أم موزع بين أكثر من مدرب.";
                    vm.DecisionExplanation = "عند تكرار الدروس الضعيفة مع مدرب بعينه، القرار هو مراجعة طريقة الشرح لا إرسال واجبات إضافية.";
                    vm.RecommendedAction = "راجع المدربين الأقل أداءً وافتح صفحة كل مدرب لمعرفة الدفعات والأسئلة عالية الخطأ.";
                    vm.PrimaryActionText = "فتح تفاصيل مدرب";
                    vm.PrimaryActionName = nameof(AnalyticsInstructorController.InstructorPerformanceDetails);
                    vm.Rows = dashboard.Instructors.OrderBy(x => x.AvgScore)
                        .Select(x => new DecisionMetricRowVM
                        {
                            Name = x.InstructorName,
                            PrimaryValue = $"{Math.Round(x.AvgScore, 1)}%",
                            SecondaryValue = $"{x.WeakLessonsCount} درس ضعيف",
                            RiskLevel = x.RiskLevel,
                            Recommendation = x.AvgScore < 70 ? "جلسة تحسين أداء" : "متابعة دورية",
                            ActionName = nameof(AnalyticsInstructorController.InstructorPerformanceDetails),
                            InstructorId = x.InstructorId
                        }).ToList();
                    return vm;

                // ── الطلاب المحللون ──────────────────────────────────────────
                case "analyzed-students":
                    vm = CreateBase(dashboard, metric,
                        "الطلاب المحللون",
                        "قراءة الطلاب الذين ظهرت لهم مؤشرات أداء قابلة لاتخاذ قرار.",
                        "bi-people-fill", "warning",
                        totalStudents.ToString(), "طالب", cards);

                    vm.Status = dashboard.CriticalCases > 0 ? "يوجد أولويات" : "لا توجد أولوية عالية";
                    vm.StatusClass = dashboard.CriticalCases > 0 ? "warning" : "success";
                    vm.DecisionSummary = "المؤشر يفرز الطلاب حسب الأداء والحضور والواجبات لتحديد من يحتاج تدخلًا.";
                    vm.DecisionExplanation = $"من أصل {totalStudents} طالب محلل، يوجد {dashboard.CriticalCases} حالة أولوية عالية.";
                    vm.RecommendedAction = "ابدأ بالحالات ذات الاختبار المنخفض ثم افحص الحضور والواجبات لتحديد نوع التدخل.";
                    vm.PrimaryActionText = "فتح تفاصيل الطلاب";
                    vm.PrimaryActionName = nameof(AnalyticsDashboardController.AnalyzedStudentsDetails);
                    vm.Rows = dashboard.Students.OrderBy(x => x.ExamScore)
                        .Select(x => new DecisionMetricRowVM
                        {
                            StudentId = x.StudentId,
                            Name = x.StudentName,
                            PrimaryValue = $"اختبار {Math.Round(x.ExamScore, 1)}%",
                            SecondaryValue = $"واجب {Math.Round(x.HomeworkScore, 1)}% | حضور {Math.Round(x.Attendance, 1)}%",
                            RiskLevel = x.Priority == "High" ? "أولوية عالية" : x.Priority == "Medium" ? "متوسط" : "مستقر",
                            Recommendation = string.IsNullOrWhiteSpace(x.ActionRequired) ? "متابعة دورية" : x.ActionRequired
                        }).ToList();
                    return vm;

                // ── حالات أولوية عالية ───────────────────────────────────────
                case "critical-cases":
                    vm = CreateBase(dashboard, metric,
                        "حالات أولوية عالية",
                        "شرح الحالات التي تحتاج قرارًا سريعًا من الإدارة أو الإشراف.",
                        "bi-person-fill-exclamation", "danger",
                        dashboard.CriticalCases.ToString(), "حالة", cards);

                    vm.Status = dashboard.CriticalCases > 5 ? "حرج" : dashboard.CriticalCases > 0 ? "تنبيه" : "مستقر";
                    vm.StatusClass = dashboard.CriticalCases > 0 ? "danger" : "success";
                    vm.DecisionSummary = "هذه الحالات هي أقصر طريق لتقليل الخطر التعليمي في الفترة الحالية.";
                    vm.DecisionExplanation = "الحالة تدخل هذه القائمة عندما تتراكم مؤشرات ضعف واضحة. متخذ القرار يحتاج السبب المباشر ثم تدخل محدد.";
                    vm.RecommendedAction = dashboard.CriticalCases > 0
                        ? "افتح تفاصيل الحالات الحرجة وحدد لكل طالب إجراء واحد قابل للتنفيذ هذا الأسبوع."
                        : "لا توجد حالات حرجة، تابع المؤشرات المتوسطة حتى لا تتحول لأولوية عالية.";
                    vm.PrimaryActionText = "فتح الحالات الحرجة";
                    vm.PrimaryActionName = nameof(AnalyticsDashboardController.CriticalStudentsDetails);
                    vm.Rows = highPriorityStudents.Select(x => new DecisionMetricRowVM
                    {
                        StudentId = x.StudentId,
                        Name = x.StudentName,
                        PrimaryValue = $"{Math.Round(x.ExamScore, 1)}%",
                        SecondaryValue = x.WeakReason,
                        RiskLevel = "أولوية عالية",
                        Recommendation = x.ActionRequired
                    }).ToList();
                    return vm;

                // ── دروس عالية الخطورة ───────────────────────────────────────
                case "high-risk-lessons":
                    vm = CreateBase(dashboard, metric,
                        "دروس عالية الخطورة",
                        "الدروس التي وصلت نسبة الضعف فيها إلى مستوى يستدعي تدخلًا تدريبيًا.",
                        "bi-exclamation-diamond-fill", "danger",
                        dashboard.HighRiskCount.ToString(), "درس", cards);

                    vm.Status = dashboard.HighRiskCount > 0 ? "تدخل مطلوب" : "لا يوجد خطر عالٍ";
                    vm.StatusClass = dashboard.HighRiskCount > 0 ? "danger" : "success";
                    vm.DecisionSummary = "الدروس عالية الخطورة تكشف موضع الخلل التعليمي بدقة أكبر من متوسط الطالب العام.";
                    vm.DecisionExplanation = "عندما يتكرر الخطأ في درس محدد لدى أكثر من طالب، فالقرار الأنسب إعادة شرح موجّه.";
                    vm.RecommendedAction = "رتب الدروس حسب نسبة الضعف، ثم افتح الدرس لمعرفة الأسئلة والطلاب المتأثرين.";
                    vm.PrimaryActionText = "فتح الدروس الحرجة";
                    vm.PrimaryActionName = nameof(AnalyticsDashboardController.HighRiskLessonsDetails);
                    vm.Rows = highRiskLessons.Select(x => new DecisionMetricRowVM
                    {
                        Name = x.LessonName,
                        Group = x.BatchName,
                        Owner = x.InstructorName,
                        PrimaryValue = $"{Math.Round(x.WeakPercentage, 1)}%",
                        SecondaryValue = $"{x.AffectedStudents} طالب متأثر",
                        RiskLevel = x.WeakPercentage >= 70 ? "خطر عالي" : "متوسط",
                        Recommendation = "إعادة شرح وقياس قصير",
                        ActionName = nameof(AnalyticsLessonController.WeakLessonDetails),
                        LessonId = x.LessonId
                    }).ToList();
                    return vm;

                // ── متوسط الضعف ──────────────────────────────────────────────
                case "avg-weakness":
                    vm = CreateBase(dashboard, metric,
                        "متوسط نسبة الضعف",
                        "تحليل الاتجاه العام للضعف عبر الدروس المحللة.",
                        "bi-bar-chart-fill", "violet",
                        Math.Round(dashboard.AvgWeakness, 1).ToString(), "%", cards);

                    vm.Status = dashboard.AvgWeakness > 60 ? "خطر" : dashboard.AvgWeakness > 40 ? "متوسط" : "مستقر";
                    vm.StatusClass = dashboard.AvgWeakness > 60 ? "danger" : dashboard.AvgWeakness > 40 ? "warning" : "success";
                    vm.DecisionSummary = "هذا المؤشر يجيب: هل المشكلة عامة في النظام أم مركزة في نقاط محددة؟";
                    vm.DecisionExplanation = "ارتفاع متوسط الضعف يعني أن التدخل يجب أن ينتقل من معالجة طالب إلى مراجعة خطة تعليمية أوسع.";
                    vm.RecommendedAction = dashboard.AvgWeakness > 60
                        ? "راجع مخاطر النظام فورًا وابدأ بأعلى الدروس والدفعات."
                        : "استخدم القائمة التفصيلية لتثبيت التحسن في الدروس المتوسطة.";
                    vm.PrimaryActionText = "فتح مخاطر النظام";
                    vm.PrimaryActionName = nameof(AnalyticsDashboardController.SystemRiskDetails);
                    vm.Rows = dashboard.Lessons.OrderByDescending(x => x.WeakPercentage).Take(20)
                        .Select(x => new DecisionMetricRowVM
                        {
                            Name = x.LessonName,
                            Group = x.BatchName,
                            Owner = x.InstructorName,
                            PrimaryValue = $"{Math.Round(x.WeakPercentage, 1)}%",
                            SecondaryValue = $"{x.TotalAttempts} محاولة",
                            RiskLevel = x.WeakPercentage >= 70 ? "خطر عالي" : x.WeakPercentage >= 50 ? "متوسط" : "منخفض",
                            Recommendation = x.WeakPercentage >= 70 ? "إعادة شرح عاجلة" : "متابعة وتحسين",
                            ActionName = nameof(AnalyticsLessonController.WeakLessonDetails),
                            LessonId = x.LessonId
                        }).ToList();
                    return vm;

                // ── المتوسطات (اختبار / واجب / حضور) ───────────────────────
                case "avg-exam":
                case "avg-homework":
                case "avg-attendance":
                    return BuildAverageMetric(metric, dashboard, cards);

                default:
                    return BuildMetricDecisionDetails("total-batches", dashboard);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        private static DecisionMetricDetailsVM BuildAverageMetric(
            string metric,
            AdvancedAnalyticsDashboardVM dashboard,
            List<DecisionMetricCardVM> cards)
        {
            bool isExam = metric == "avg-exam";
            bool isHomework = metric == "avg-homework";

            string title = isExam ? "متوسط الاختبارات"
                         : isHomework ? "متوسط الواجبات"
                                      : "متوسط الحضور";

            double value = isExam ? dashboard.AvgExamScore
                         : isHomework ? dashboard.AvgHomeworkScore
                                      : dashboard.AvgAttendance;

            string icon = isExam ? "bi-pencil-square"
                         : isHomework ? "bi-journal-check"
                                      : "bi-calendar2-check";

            string theme = isExam ? "danger"
                         : isHomework ? "warning"
                                      : "success";

            var vm = CreateBase(dashboard, metric, title,
                "شرح أثر هذا المتوسط على قرار المتابعة والتحسين.",
                icon, theme, Math.Round(value, 1).ToString(), "%", cards);

            vm.Status = value >= 70 ? "جيد" : value >= 50 ? "متوسط" : "ضعيف";
            vm.StatusClass = value >= 70 ? "success" : value >= 50 ? "warning" : "danger";

            vm.DecisionSummary = isExam
                ? "متوسط الاختبارات يعكس مستوى الإتقان بعد التعلم."
                : isHomework
                    ? "متوسط الواجبات يعكس الالتزام والتدريب بين الحصص."
                    : "متوسط الحضور يوضح هل انخفاض الأداء مرتبط بالتغيب.";

            vm.DecisionExplanation = value >= 70
                ? "المؤشر جيد إجمالًا، والقرار هو الحفاظ على المستوى مع معالجة الحالات الفردية الضعيفة."
                : value >= 50
                    ? "المؤشر متوسط ويحتاج متابعة موجهة قبل أن يتحول إلى خطر."
                    : "المؤشر ضعيف ويحتاج تدخلًا مباشرًا بخطة قصيرة قابلة للقياس.";

            vm.RecommendedAction = isExam
                ? "راجع أقل الطلاب في الاختبارات وحدد الدروس المتكررة في الخطأ."
                : isHomework
                    ? "راجع الطلاب الأقل التزامًا بالواجب وحدد واجبًا علاجيًا قصيرًا."
                    : "راجع الطلاب منخفضي الحضور واربط القرار بخطة تعويض أو تنبيه إداري.";

            vm.PrimaryActionText = "فتح تفاصيل الطلاب";
            vm.PrimaryActionName = nameof(AnalyticsDashboardController.AnalyzedStudentsDetails);

            vm.Rows = dashboard.Students
                .OrderBy(x => isExam ? x.ExamScore : isHomework ? x.HomeworkScore : x.Attendance)
                .Take(20)
                .Select(x => new DecisionMetricRowVM
                {
                    StudentId = x.StudentId,
                    Name = x.StudentName,
                    PrimaryValue = isExam ? $"{Math.Round(x.ExamScore, 1)}%"
                                   : isHomework ? $"{Math.Round(x.HomeworkScore, 1)}%"
                                                : $"{Math.Round(x.Attendance, 1)}%",
                    SecondaryValue = $"اختبار {Math.Round(x.ExamScore, 1)}% | واجب {Math.Round(x.HomeworkScore, 1)}% | حضور {Math.Round(x.Attendance, 1)}%",
                    RiskLevel = x.Priority == "High" ? "أولوية عالية" : x.Priority == "Medium" ? "متوسط" : "مستقر",
                    Recommendation = string.IsNullOrWhiteSpace(x.ActionRequired) ? vm.RecommendedAction : x.ActionRequired
                }).ToList();

            return vm;
        }

        // ──────────────────────────────────────────────────────────────────────
        private static DecisionMetricDetailsVM CreateBase(
            AdvancedAnalyticsDashboardVM dashboard,
            string metric, string title, string subtitle,
            string icon, string theme, string value, string unit,
            List<DecisionMetricCardVM> cards)
        {
            return new DecisionMetricDetailsVM
            {
                MetricKey = metric,
                Title = title,
                Subtitle = subtitle,
                Icon = icon,
                Theme = theme,
                Value = value,
                Unit = unit,
                Cards = cards,
                SelectedCurriculumId = dashboard.SelectedCurriculumId,
                SelectedBatchId = dashboard.SelectedBatchId,
                SelectedInstructorId = dashboard.SelectedInstructorId,
                InstructorsFilter = dashboard.InstructorsFilter
            };
        }
    }
}
