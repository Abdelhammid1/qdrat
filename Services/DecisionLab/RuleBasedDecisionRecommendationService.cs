using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public class RuleBasedDecisionRecommendationService : IDecisionRecommendationService
    {
        private const string ExperimentalWarning =
            "هذه توصية تجريبية من DecisionLab ولا يجب تنفيذها إلا بعد مراجعة واعتماد مسؤول.";

        public Task<IReadOnlyList<DecisionLabRecommendationPreviewViewModel>> BuildRecommendationsAsync(
            DecisionLabBatchAnalysisViewModel batchAnalysis)
        {
            IReadOnlyList<DecisionLabRecommendationPreviewViewModel> recommendations =
                new List<DecisionLabRecommendationPreviewViewModel>
                {
                    BuildRecommendation(batchAnalysis)
                };

            return Task.FromResult(recommendations);
        }

        public Task<DecisionLabRecommendationDetailsViewModel> BuildRecommendationDetailsAsync(
            DecisionLabBatchAnalysisViewModel batchAnalysis,
            string recommendationCode)
        {
            var preview = BuildRecommendation(batchAnalysis);

            var details = new DecisionLabRecommendationDetailsViewModel
            {
                Code = preview.Code,
                Title = preview.Title,
                Category = preview.Category,
                Priority = preview.Priority,
                Summary = preview.Reason,
                Rationale = BuildRationale(batchAnalysis),
                SuggestedAction = preview.SuggestedAction,
                RequiresApproval = true,
                CanCreateExamDraft = preview.CanCreateExamDraft,
                RiskBreakdown = batchAnalysis.RiskBreakdown,
                RelatedWeakLessons = batchAnalysis.WeakLessons.Take(5).ToList(),
                RelatedHighRiskQuestions = batchAnalysis.HighRiskQuestions.Take(10).ToList()
            };

            details.EvidencePoints.Add($"مستوى خطر الدفعة: {batchAnalysis.RiskLevel}");
            details.EvidencePoints.Add($"متوسط الحضور: {batchAnalysis.AttendancePercent:0.#}%");
            details.EvidencePoints.Add($"متوسط الواجبات: {batchAnalysis.AverageHomeworkScore:0.#}%");
            details.EvidencePoints.Add($"متوسط الاختبارات: {batchAnalysis.AverageExamScore:0.#}%");
            details.EvidencePoints.Add($"عدد المحاولات المحللة: {batchAnalysis.TotalQuestionAttempts}");

            return Task.FromResult(details);
        }

        private static DecisionLabRecommendationPreviewViewModel BuildRecommendation(
            DecisionLabBatchAnalysisViewModel batchAnalysis)
        {
            var interventionType = SelectInterventionType(batchAnalysis);
            var priority = SelectPriority(batchAnalysis.RiskScore, batchAnalysis.RiskLevel);

            return new DecisionLabRecommendationPreviewViewModel
            {
                BatchId = batchAnalysis.BatchId,
                CurriculumId = batchAnalysis.CurriculumId,
                Code = $"DL-{interventionType}",
                Title = BuildTitle(interventionType),
                Category = "DecisionLab",
                Priority = priority,
                InterventionType = interventionType,
                Reason = BuildReason(batchAnalysis, interventionType),
                SuggestedAction = BuildSuggestedAction(interventionType),
                WarningMessage = ExperimentalWarning,
                ImpactScore = ClampImpactScore(batchAnalysis.RiskScore),
                RequiresApproval = true,
                CanCreateExamDraft = interventionType == "ShortRemedialExam"
            };
        }

        private static string SelectInterventionType(DecisionLabBatchAnalysisViewModel batchAnalysis)
        {
            if (batchAnalysis.TotalQuestionAttempts == 0)
            {
                return "DataReview";
            }

            var hasAttendanceRisk = batchAnalysis.AttendancePercent > 0 && batchAnalysis.AttendancePercent < 70;
            var hasHomeworkRisk = batchAnalysis.SentHomeworkSetsCount > 0
                && (batchAnalysis.HomeworkSubmissionPercent < 70 || batchAnalysis.AverageHomeworkScore < 60);
            var hasContentRisk = batchAnalysis.HighRiskQuestionsCount >= 5 || batchAnalysis.WeakLessonsCount >= 3;
            var hasPerformanceRisk = batchAnalysis.SentExamsCount > 0
                && (batchAnalysis.ExamParticipationPercent < 70 || batchAnalysis.AverageExamScore < 60);

            var riskSignalsCount = 0;
            if (hasAttendanceRisk) riskSignalsCount++;
            if (hasHomeworkRisk) riskSignalsCount++;
            if (hasContentRisk) riskSignalsCount++;
            if (hasPerformanceRisk) riskSignalsCount++;

            if (riskSignalsCount >= 2)
            {
                return "MixedIntervention";
            }

            if (hasAttendanceRisk)
            {
                return "AttendanceFollowUp";
            }

            if (hasHomeworkRisk)
            {
                return "HomeworkSupport";
            }

            if (hasContentRisk)
            {
                return "ShortRemedialExam";
            }

            if (hasPerformanceRisk)
            {
                return "InstructorGuidance";
            }

            return "MonitorOnly";
        }

        private static string SelectPriority(double riskScore, string riskLevel)
        {
            if (riskScore >= 75 || riskLevel == "حرج")
            {
                return "عاجل";
            }

            if (riskScore >= 55 || riskLevel == "مرتفع")
            {
                return "مرتفع";
            }

            if (riskScore >= 30 || riskLevel == "متوسط")
            {
                return "متوسط";
            }

            return "منخفض";
        }

        private static string BuildTitle(string interventionType)
        {
            return interventionType switch
            {
                "DataReview" => "استكمال بيانات الدفعة قبل اتخاذ قرار",
                "AttendanceFollowUp" => "متابعة حضور الدفعة",
                "HomeworkSupport" => "دعم الواجبات والتسليم",
                "ShortRemedialExam" => "مراجعة مركزة للأسئلة والمؤشرات",
                "InstructorGuidance" => "مراجعة أداء الاختبارات",
                "MixedIntervention" => "تدخل مركب للدفعة",
                _ => "متابعة دورية دون تدخل مباشر"
            };
        }

        private static string BuildReason(
            DecisionLabBatchAnalysisViewModel batchAnalysis,
            string interventionType)
        {
            return interventionType switch
            {
                "DataReview" => "لا توجد محاولات أسئلة كافية لبناء توصية تشغيلية موثوقة.",
                "AttendanceFollowUp" => $"متوسط الحضور الحالي {batchAnalysis.AttendancePercent:0.#}% وهو أقل من المستوى المقبول.",
                "HomeworkSupport" => $"نسبة تسليم الواجبات {batchAnalysis.HomeworkSubmissionPercent:0.#}% ومتوسط الأداء {batchAnalysis.AverageHomeworkScore:0.#}%.",
                "ShortRemedialExam" => $"تم رصد {batchAnalysis.WeakLessonsCount} مؤشرات ضعيفة و{batchAnalysis.HighRiskQuestionsCount} أسئلة عالية الخطأ.",
                "InstructorGuidance" => $"متوسط الاختبارات {batchAnalysis.AverageExamScore:0.#}% ونسبة المشاركة {batchAnalysis.ExamParticipationPercent:0.#}%.",
                "MixedIntervention" => $"تظهر أكثر من إشارة خطر داخل الدفعة بمستوى خطر {batchAnalysis.RiskLevel}.",
                _ => "مؤشرات الدفعة لا تستدعي تدخلاً الآن، مع استمرار المتابعة."
            };
        }

        private static string BuildSuggestedAction(string interventionType)
        {
            return interventionType switch
            {
                "DataReview" => "راجع اكتمال بيانات المحاولات والحضور والواجبات قبل إصدار قرار.",
                "AttendanceFollowUp" => "تواصل مع مشرف الدفعة وحدد الطلاب الأكثر غيابًا من التقارير الحالية.",
                "HomeworkSupport" => "خصص مراجعة قصيرة للواجبات غير المتقنة قبل فتح نشاط جديد.",
                "ShortRemedialExam" => "جهز مراجعة مركزة حول أضعف المؤشرات والأسئلة الأعلى خطأ.",
                "InstructorGuidance" => "راجع نتائج الاختبارات وحدد هل المشكلة مشاركة منخفضة أم أداء معرفي.",
                "MixedIntervention" => "اجمع بين متابعة الحضور ودعم الواجبات ومراجعة المؤشرات حسب سبب الخطر.",
                _ => "استمر في المراقبة الدورية دون إنشاء تدخل جديد."
            };
        }

        private static string BuildRationale(DecisionLabBatchAnalysisViewModel batchAnalysis)
        {
            return
                $"تم اختيار التوصية بناءً على RiskScore={batchAnalysis.RiskScore:0.#}، " +
                $"الحضور={batchAnalysis.AttendancePercent:0.#}%، " +
                $"الواجبات={batchAnalysis.AverageHomeworkScore:0.#}%، " +
                $"الاختبارات={batchAnalysis.AverageExamScore:0.#}%، " +
                $"وعدد المحاولات={batchAnalysis.TotalQuestionAttempts}.";
        }

        private static int ClampImpactScore(double riskScore)
        {
            if (riskScore < 0)
            {
                return 0;
            }

            if (riskScore > 100)
            {
                return 100;
            }

            return (int)Math.Round(riskScore, MidpointRounding.AwayFromZero);
        }
    }
}
