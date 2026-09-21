using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Implementations
{
    public class ExamRecommendationService : IExamRecommendationService
    {
        // 🔹 النسخة التربوية المحسّنة
        public ExamRecommendationVm GetRecommendation(double overallPercent, int solveMinutes, double totalMinutes)
        {
            return GetRecommendation(overallPercent, solveMinutes, totalMinutes, null);
        }

        // ✅ النسخة الجديدة التربوية المحسّنة
        public ExamRecommendationVm GetRecommendation(double overallPercent, int solveMinutes, double totalMinutes, ExamType? examType = null)
        {
            var result = new ExamRecommendationVm();

            double percentTime = totalMinutes > 0 ? Math.Round((solveMinutes / totalMinutes) * 100, 1) : 0;

            // 🧭 تحليل الزمن والسرعة
            result.SpeedLabel = percentTime < 30 ? "سرعة عالية"
                               : percentTime < 70 ? "توازن جيد"
                               : "حل متأنٍ";

            // 🧠 تحليل الأداء بناءً على العلاقة بين الدقة والسرعة
            if (percentTime < 30)
            {
                result.SpeedNote = overallPercent < 60
                    ? "تعمل بسرعة كبيرة، لكنك بحاجة إلى قليل من التروي في مراجعة إجاباتك قبل الإرسال."
                    : "سرعتك ممتازة! استمر في التوازن بين التفكير السريع والدقة في الحل.";
            }
            else if (percentTime < 70)
            {
                result.SpeedNote = overallPercent >= 70
                    ? "إدارة وقتك متوازنة جدًا 👏 — حافظ على هذا النمط أثناء الاختبارات القادمة."
                    : "حلك متزن زمنيًا، حاول فقط رفع مستوى الدقة عبر مراجعة المفاهيم الصعبة.";
            }
            else
            {
                result.SpeedNote = overallPercent >= 80
                    ? "أداؤك دقيق ومركّز، لكن يمكنك تسريع وتيرة الحل قليلًا دون فقدان التركيز."
                    : "تحتاج إلى زيادة سرعة اتخاذ القرار أثناء الحل التدريبي لتحسين وقت الإنجاز.";
            }

            // 💬 توصيات سلوكية حسب نوع الاختبار
            switch (examType)
            {
                case ExamType.LevelAssessment:
                    result.TrackCard1 = "خطوة البداية";
                    result.TrackCard1Desc = overallPercent < 50
                        ? "ابدأ بتقوية مهاراتك الأساسية في الكمي واللفظي عبر تمارين قصيرة يومية."
                        : "أنت في مستوى جيد! يمكنك الانتقال إلى الدروس التطبيقية لزيادة الدقة.";
                    result.TrackCard2 = "الخطة التالية";
                    result.TrackCard2Desc = overallPercent < 80
                        ? "استمر في التدريب المنتظم لبلوغ 80% قبل الانتقال إلى الاختبارات المتقدمة."
                        : "جاهز للدورات الاحترافية والتحديات الزمنية المتقدمة.";
                    break;

                case ExamType.PerformanceScale:
                    result.TrackCard1 = "تحليل مؤشراتك الحالية";
                    result.TrackCard1Desc = "راجع المحاور ذات الدقة الأقل من 70% وضع خطة قصيرة لتحسينها.";
                    result.TrackCard2 = "خطة التعزيز";
                    result.TrackCard2Desc = "استهدف رفع مؤشرات الأداء تدريجيًا عبر حل نماذج أسبوعية قصيرة.";
                    break;

                case ExamType.SectionExam:
                    result.TrackCard1 = "مراجعة المحور";
                    result.TrackCard1Desc = "ركّز على الدروس التي كانت نتائجها أقل من 70% وأعد حل أسئلتها بتركيز.";
                    result.TrackCard2 = "التدرّب المتقدم";
                    result.TrackCard2Desc = "بعد الوصول إلى 80%، جرّب اختبارات المحور الكاملة بالمؤقت.";
                    break;

                case ExamType.CurriculumFinal:
                    result.TrackCard1 = "التقييم العام";
                    result.TrackCard1Desc = "هذا الاختبار يقيس مدى جاهزيتك، فراجع نقاط ضعفك قبل الانتقال للدورة التالية.";
                    result.TrackCard2 = "مرحلة التطوير";
                    result.TrackCard2Desc = "استخدم تقرير التحليل لتحديد مهارات تحتاج لتعزيزها في خطتك القادمة.";
                    break;

                case ExamType.Manual:
                    result.TrackCard1 = "تحليل النماذج";
                    result.TrackCard1Desc = "قارن بين أدائك في النماذج المختلفة واكتشف الأسئلة المتكررة.";
                    result.TrackCard2 = "تمارين الضغط الزمني";
                    result.TrackCard2Desc = "حل نموذجًا كاملًا بوقت محدد لتقوية الاستجابة تحت الضغط.";
                    break;

                case ExamType.LessonSkillsReinforcement:
                    result.TrackCard1 = "تعزيز المهارات";
                    result.TrackCard1Desc = "راجع الدروس المرتبطة بالمهارة التي أخطأت فيها وأعد المحاولة.";
                    result.TrackCard2 = "تثبيت الأداء";
                    result.TrackCard2Desc = "حاول رفع دقة الحل في هذه المهارة إلى أكثر من 90%.";
                    break;

                default:
                    result.TrackCard1 = "خطوات التطوير";
                    result.TrackCard1Desc = "استمر في حل نماذج قصيرة يوميًا لرفع معدل الدقة والسرعة معًا.";
                    result.TrackCard2 = "التدرّب الاحترافي";
                    result.TrackCard2Desc = "عند بلوغ 80% يمكنك الانتقال إلى اختبارات المحاكاة الزمنية.";
                    break;
            }

            // 🧩 الملخص العام والنصائح الإضافية
            result.PerformanceSummary = $"{result.SpeedLabel} — {result.SpeedNote}";

            // نصائح فردية واقعية
            if (percentTime < 30)
            {
                result.IndividualTips.Add("⚡ سرعتك عالية! جرّب التمهل قليلاً لضمان مراجعة دقيقة قبل الإرسال.");
                result.IndividualTips.Add("📖 خصص دقيقة إضافية لكل سؤال للتحقق من منطق الإجابة.");
            }
            else if (percentTime > 80)
            {
                result.IndividualTips.Add("🐢 تحتاج إلى تحسين سرعة اتخاذ القرار أثناء الحل.");
                result.IndividualTips.Add("⏱️ استخدم المؤقت التدريبي لتعتاد على الحل ضمن وقت محدد.");
            }
            else
            {
                result.IndividualTips.Add("✅ توازنك بين السرعة والدقة رائع، استمر بنفس الإيقاع.");
                result.IndividualTips.Add("💡 ركّز على الأسئلة الصعبة تدريجيًا لرفع مستوى الثقة والأداء.");
            }

            result.IndividualTips.Add($"⏲️ النسبة الزمنية الحالية: {percentTime}% من مدة الاختبار.");
            result.IndividualTips.Add("📊 تابع تغير أدائك في لوحة التحليل لمعرفة مدى تطورك.");

            return result;
        }
    }
}
