using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Implementations
{
    public class BatchPerformanceRecommendationService : IBatchPerformanceRecommendationService
    {
        public List<BatchRecommendationVM> GenerateRecommendations(BatchPerformanceReportVM report)
        {
            var result = new List<BatchRecommendationVM>();

            if (report.TotalHomeworksInPeriod == 0 && report.TotalLecturesInPeriod == 0)
                return result;

            var submissionRate = report.OverallSubmissionRate;
            var avgScore = report.OverallAverageScore;
            var attendanceRate = report.OverallAttendanceRate;
            var countNotSubmitted = report.CountNotSubmitted;
            var countBelow60 = report.CountBelow60;

            bool hasIssues = false;

            // ── نسبة التسليم ──
            if (report.TotalHomeworksInPeriod > 0)
            {
                if (submissionRate < 50)
                {
                    hasIssues = true;
                    result.Add(new BatchRecommendationVM
                    {
                        Icon = "bi-exclamation-triangle-fill",
                        Severity = "critical",
                        Title = "نسبة تسليم منخفضة جداً",
                        Message = $"نسبة تسليم الواجبات خلال هذه الفترة منخفضة جدًا ({submissionRate:F0}%) — يُنصح بالتواصل المباشر مع الطلاب المتأخرين وتذكيرهم عبر القنوات المتاحة."
                    });
                }
                else if (submissionRate < 75)
                {
                    hasIssues = true;
                    result.Add(new BatchRecommendationVM
                    {
                        Icon = "bi-bell-fill",
                        Severity = "warning",
                        Title = "نسبة تسليم متوسطة",
                        Message = $"نسبة التسليم متوسطة ({submissionRate:F0}%) — يُفضّل إرسال تنبيه جماعي للدفعة قبل قرب انتهاء موعد الواجب."
                    });
                }

                // ── متوسط الدرجات ──
                if (avgScore < 60)
                {
                    hasIssues = true;
                    result.Add(new BatchRecommendationVM
                    {
                        Icon = "bi-graph-down-arrow",
                        Severity = "critical",
                        Title = "متوسط درجات منخفض",
                        Message = $"متوسط درجات الدفعة ({avgScore:F0}%) أقل من الحد المطلوب (60%) — يُنصح بعقد حصة مراجعة مكثّفة لأهم المحاور الضعيفة قبل الواجب القادم."
                    });
                }
                else if (avgScore < 75)
                {
                    hasIssues = true;
                    result.Add(new BatchRecommendationVM
                    {
                        Icon = "bi-clipboard2-pulse",
                        Severity = "warning",
                        Title = "متوسط الدرجات يحتاج تحسين",
                        Message = $"متوسط الدرجات ({avgScore:F0}%) مقبول لكنه يحتاج تحسين — يُنصح بتخصيص وقت إضافي لمراجعة الأسئلة الأكثر خطأً."
                    });
                }
            }

            // ── نسبة الحضور ──
            if (report.TotalLecturesInPeriod > 0 && attendanceRate < 70)
            {
                hasIssues = true;
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-calendar-x",
                    Severity = "warning",
                    Title = "نسبة حضور منخفضة",
                    Message = $"نسبة الحضور خلال هذه الفترة ({attendanceRate:F0}%) منخفضة — يُنصح بمتابعة أسباب الغياب مع الطلاب المتكررين."
                });
            }

            // ── طلاب لم يسلّموا ──
            if (countNotSubmitted > 0)
            {
                hasIssues = true;
                var names = string.Join("، ", report.StudentsNotSubmitted.Select(s => s.FullName));
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-person-x-fill",
                    Severity = "critical",
                    Title = "طلاب لم يسلّموا أي واجب",
                    Message = $"هناك {countNotSubmitted} طالب لم يسلّم أي واجب خلال هذه الفترة: {names}. يُنصح بالتواصل الفردي معهم أو مع أولياء أمورهم."
                });
            }

            // ── طلاب أقل من 60% ──
            if (countBelow60 > 0)
            {
                hasIssues = true;
                var names = string.Join("، ", report.StudentsBelow60.Select(s => s.FullName));
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-person-fill-exclamation",
                    Severity = "warning",
                    Title = "طلاب بمستوى أقل من 60%",
                    Message = $"هناك {countBelow60} طالب حصلوا على أقل من 60% في واجبات هذه الفترة: {names}. يُنصح بمتابعتهم بشكل فردي أو تكوين مجموعة دعم لهم."
                });
            }

            // ── طلاب في منطقة الخطر العالي ──
            var highRisk = report.StudentsRisk.Where(r => r.AtRiskIndex >= 60).Take(3).ToList();
            if (highRisk.Any())
            {
                hasIssues = true;
                var names = string.Join("، ", highRisk.Select(r => $"{r.FullName} ({r.AtRiskIndex:F0}%)"));
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-exclamation-octagon-fill",
                    Severity = "critical",
                    Title = "طلاب في منطقة الخطر العالي",
                    Message = $"الطلاب التالية أسماؤهم لديهم مؤشر خطورة مرتفع ويحتاجون تدخلًا عاجلًا: {names}. يُنصح بالتواصل المباشر معهم وأولياء أمورهم خلال 24 ساعة."
                });
            }

            // ── أضعف محاضرة ──
            var weakestLecture = report.Lectures.Where(l => l.AttendanceRate < 70).OrderBy(l => l.AttendanceRate).FirstOrDefault();
            if (weakestLecture != null)
            {
                hasIssues = true;
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-camera-video-off",
                    Severity = "warning",
                    Title = "محاضرة بحضور منخفض",
                    Message = $"محاضرة \"{weakestLecture.Title}\" بتاريخ {weakestLecture.Date:yyyy/MM/dd} سجّلت أدنى نسبة حضور ({weakestLecture.AttendanceRate:F0}%) — يُنصح بمتابعة أسباب الغياب في هذا اليوم تحديدًا."
                });
            }

            // ── أضعف واجب ──
            if (report.Homeworks.Any())
            {
                var weakestHw = report.Homeworks
                    .OrderBy(h => h.SubmissionRate)
                    .ThenBy(h => h.AverageScore)
                    .First();
                if (weakestHw.SubmissionRate < 60 || weakestHw.AverageScore < 60)
                {
                    hasIssues = true;
                    result.Add(new BatchRecommendationVM
                    {
                        Icon = "bi-file-earmark-x",
                        Severity = "warning",
                        Title = "واجب يحتاج مراجعة",
                        Message = $"واجب \"{weakestHw.Title}\" سجّل نسبة تسليم {weakestHw.SubmissionRate:F0}% ومتوسط درجة {weakestHw.AverageScore:F0}% — يُنصح بمراجعة مستوى الصعوبة أو تقديم شرح إضافي حوله."
                    });
                }
            }

            // ── أضعف منهج/مدرّب ──
            var weakestCurriculum = report.CurriculumBreakdown
                .Where(c => c.HomeworkCount > 0 && c.AverageScore < 60)
                .OrderBy(c => c.AverageScore)
                .FirstOrDefault();
            if (weakestCurriculum != null)
            {
                hasIssues = true;
                var instructorPart = !string.IsNullOrEmpty(weakestCurriculum.InstructorName)
                    ? $" (المدرّب: {weakestCurriculum.InstructorName})"
                    : "";
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-book-half",
                    Severity = "warning",
                    Title = "منهج يحتاج خطة علاجية",
                    Message = $"منهج \"{weakestCurriculum.CurriculumTitle}\"{instructorPart} سجّل أدنى متوسط درجات ({weakestCurriculum.AverageScore:F0}%) — يُنصح بالتنسيق مع المدرّب المسؤول لوضع خطة مراجعة مكثّفة."
                });
            }

            // ── اتجاه التراجع/التحسّن مقارنة بالفترة السابقة ──
            if (report.Comparison.AverageScore.Trend == "down" && report.Comparison.AverageScore.Delta <= -5)
            {
                hasIssues = true;
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-graph-down",
                    Severity = "critical",
                    Title = "تراجع ملحوظ في الأداء عن الفترة السابقة",
                    Message = $"متوسط الدرجات تراجع من {report.Comparison.AverageScore.PreviousValue:F0}% إلى {report.Comparison.AverageScore.CurrentValue:F0}% — يُنصح بمراجعة سبب التراجع قبل الاستمرار في المنهج."
                });
            }
            else if (report.Comparison.AverageScore.Trend == "up" && report.Comparison.AverageScore.Delta >= 5)
            {
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-graph-up",
                    Severity = "info",
                    Title = "تحسّن ملحوظ في الأداء",
                    Message = $"متوسط الدرجات تحسّن من {report.Comparison.AverageScore.PreviousValue:F0}% إلى {report.Comparison.AverageScore.CurrentValue:F0}% — استمر بنفس الأسلوب."
                });
            }

            // ── كل شيء جيد ──
            if (!hasIssues)
            {
                result.Add(new BatchRecommendationVM
                {
                    Icon = "bi-check-circle-fill",
                    Severity = "info",
                    Title = "أداء جيد عام",
                    Message = "أداء الدفعة خلال هذه الفترة جيد بشكل عام — استمر بنفس الوتيرة، ويمكن التركيز على رفع مستوى الطلاب القريبين من 60% ليتجاوزوه."
                });
            }

            return result;
        }

        public List<string> BuildWeeklyActionPlan(BatchPerformanceReportVM report)
        {
            var plan = new List<string>();

            // الأولوية 1: طلاب الخطر العالي
            var highRisk = report.StudentsRisk.Where(r => r.AtRiskIndex >= 60).Take(3).ToList();
            if (highRisk.Any())
            {
                var names = string.Join("، ", highRisk.Select(r => r.FullName));
                plan.Add($"تواصل مع {names} وأولياء أمورهم خلال 24 ساعة لمعرفة أسباب التأخر وتقديم الدعم اللازم.");
            }

            // الأولوية 2: طلاب لم يسلّموا
            if (report.CountNotSubmitted > 0 && !highRisk.Any(r => report.StudentsNotSubmitted.Any(s => s.StudentId == r.StudentId)))
            {
                var names = string.Join("، ", report.StudentsNotSubmitted.Take(3).Select(s => s.FullName));
                plan.Add($"ذكّر {names}{(report.CountNotSubmitted > 3 ? " وآخرين" : "")} بضرورة تسليم الواجبات المتأخرة وحدّد موعدًا أقصى للتسليم.");
            }

            // الأولوية 3: أضعف محاضرة
            var weakestLecture = report.Lectures.Where(l => l.AttendanceRate < 70).OrderBy(l => l.AttendanceRate).FirstOrDefault();
            if (weakestLecture != null)
                plan.Add($"أعد شرح محتوى محاضرة \"{weakestLecture.Title}\" بأسلوب مبسّط في الحصة القادمة نظرًا لانخفاض نسبة الحضور.");

            // الأولوية 4: أضعف واجب
            if (report.Homeworks.Any())
            {
                var weakestHw = report.Homeworks.OrderBy(h => h.SubmissionRate).ThenBy(h => h.AverageScore).First();
                if (weakestHw.SubmissionRate < 60 || weakestHw.AverageScore < 60)
                    plan.Add($"راجع واجب \"{weakestHw.Title}\" مع الطلاب وحدّد نقاط الضعف الأكثر شيوعًا لمعالجتها.");
            }

            // الأولوية 5: أضعف منهج
            var weakestCurriculum = report.CurriculumBreakdown.Where(c => c.HomeworkCount > 0).OrderBy(c => c.AverageScore).FirstOrDefault();
            if (weakestCurriculum != null && weakestCurriculum.AverageScore < 70 && plan.Count < 5)
            {
                var instructorPart = !string.IsNullOrEmpty(weakestCurriculum.InstructorName)
                    ? $" بالتنسيق مع المدرّب {weakestCurriculum.InstructorName}"
                    : "";
                plan.Add($"ضع خطة مراجعة لمنهج \"{weakestCurriculum.CurriculumTitle}\"{instructorPart} خلال هذا الأسبوع.");
            }

            // إن لا توجد مشاكل: خطوات إيجابية
            if (!plan.Any())
            {
                plan.Add("حافظ على وتيرة التسليم والحضور الحالية وشجّع الطلاب على الاستمرار.");
                if (report.TopPerformers.Any())
                    plan.Add($"كرّم الطلاب المتميزين وفي مقدمتهم {report.TopPerformers.First().FullName} أمام زملائهم لتعزيز الدافعية.");
                if (report.CurriculumBreakdown.Any(c => c.HomeworkCount > 0))
                {
                    var best = report.CurriculumBreakdown.Where(c => c.HomeworkCount > 0).OrderByDescending(c => c.AverageScore).First();
                    plan.Add($"استمر بنفس أسلوب تدريس منهج \"{best.CurriculumTitle}\" الذي سجّل أعلى متوسط درجات ({best.AverageScore:F0}%).");
                }
            }

            return plan.Take(5).ToList();
        }
    }
}
