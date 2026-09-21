using System;

namespace QdratNew.Helpers
{
    /// <summary>
    /// نوع الاختبار — يؤثر على صياغة الرسائل التحفيزية.
    /// </summary>
    public enum ResultContext
    {
        Exam,
        Homework,
        Placement,
        PerformanceIndicator
    }

    /// <summary>
    /// حزمة نبرة موحّدة لعرض نتائج الطالب.
    /// الهدف: نقل الطالب من إحساس "الفشل النهائي" إلى "نقطة انطلاق + خطوة قادمة".
    /// لا تُستخدم كلمات: فشل / راسب / لم تنجح في أي نص يراه الطالب أو ولي الأمر.
    /// </summary>
    public class ResultTone
    {
        /// <summary>excellent | strong | onTrack | start</summary>
        public string Tier { get; set; } = "start";

        /// <summary>theme-excellent | theme-good | theme-acceptable | theme-growth</summary>
        public string ThemeCss { get; set; } = "theme-growth";

        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";

        public string BadgeText { get; set; } = "";
        /// <summary>er-badge--pass | er-badge--progress</summary>
        public string BadgeCss { get; set; } = "er-badge--progress";

        public string MotivationHeading { get; set; } = "";
        public string MotivationMsg { get; set; } = "";

        /// <summary>احتفال (ألعاب نارية) — فقط للتفوّق.</summary>
        public bool ShowCelebration { get; set; }

        /// <summary>الشريحة الأدنى (أقل من حد الاجتياز) — نستخدمها لإظهار مسار التقوية بدل الإنذار.</summary>
        public bool IsLowTier { get; set; }

        /// <summary>عنوان زر الخطوة القادمة.</summary>
        public string NextStepLabel { get; set; } = "ابدأ خطة التقوية";

        /// <summary>أيقونة Bootstrap محايدة (للصفحات القديمة التي تعتمد bi-*).</summary>
        public string IconCss { get; set; } = "bi-graph-up-arrow text-primary";
    }

    public static class ResultToneHelper
    {
        /// <summary>
        /// يبني نبرة النتيجة حسب النسبة المئوية ونوع الاختبار.
        /// </summary>
        /// <param name="percentage">النسبة المئوية 0..100</param>
        /// <param name="context">نوع الاختبار</param>
        /// <param name="passThreshold">حد الاجتياز (افتراضي 50). للاختبارات التشخيصية يُتجاهل.</param>
        public static ResultTone Build(double percentage, ResultContext context = ResultContext.Exam, double passThreshold = 50)
        {
            var pct = Math.Max(0, Math.Min(100, percentage));

            bool isExcellent = pct >= 90;
            bool isStrong = pct >= 70 && pct < 90;
            bool isOnTrack = pct >= passThreshold && pct < 70;
            bool isStart = pct < passThreshold;

            // الاختبارات التشخيصية: لا يوجد نجاح/رسوب — النتيجة تحدد نقطة البداية فقط.
            bool isDiagnostic = context == ResultContext.Placement || context == ResultContext.PerformanceIndicator;

            var t = new ResultTone
            {
                ShowCelebration = isExcellent,
                IsLowTier = isStart
            };

            if (isExcellent)
            {
                t.Tier = "excellent";
                t.ThemeCss = "theme-excellent";
                t.IconCss = "bi-trophy-fill text-warning";
                t.Title = "رائع! أنت متفوّق حقاً";
                t.Subtitle = "هذه النتيجة تعكس جهداً حقيقياً واجتهاداً مستمراً — حافظ على هذا المستوى.";
                t.BadgeText = isDiagnostic ? "مستوى متقدّم" : "✔ اجتزت بامتياز";
                t.BadgeCss = "er-badge--pass";
                t.MotivationHeading = "استمر في القمة!";
                t.MotivationMsg = "🏆 أداء استثنائي. ركّز على تثبيت هذا المستوى في كل محور.";
                t.NextStepLabel = "راجع إجاباتك";
            }
            else if (isStrong)
            {
                t.Tier = "strong";
                t.ThemeCss = "theme-good";
                t.IconCss = "bi-emoji-smile-fill text-success";
                t.Title = "أحسنت! نتيجة قوية";
                t.Subtitle = "أداؤك جيد جداً، ومع مراجعة بسيطة للأخطاء ستصل إلى القمة.";
                t.BadgeText = isDiagnostic ? "مستوى جيد" : "✔ اجتزت الاختبار";
                t.BadgeCss = "er-badge--pass";
                t.MotivationHeading = "خطوة واحدة عن التميّز";
                t.MotivationMsg = "💡 راجع المحاور التي أخطأت فيها — نتيجتك القادمة ستكون أعلى.";
                t.NextStepLabel = "راجع المحاور";
            }
            else if (isOnTrack)
            {
                t.Tier = "onTrack";
                t.ThemeCss = "theme-acceptable";
                t.IconCss = "bi-graph-up-arrow text-info";
                t.Title = "على الطريق الصحيح 📈";
                t.Subtitle = "لديك قاعدة جيدة. المراجعة المنتظمة للمحاور ستنقلك للمستوى الأعلى بسرعة.";
                t.BadgeText = isDiagnostic ? "مستوى متوسط" : "✔ اجتزت الحد";
                t.BadgeCss = "er-badge--pass";
                t.MotivationHeading = "تقدّمك واضح — أكمل";
                t.MotivationMsg = "📚 راجع الدروس المرتبطة بالأسئلة الخاطئة، ثم أعد المحاولة.";
                t.NextStepLabel = "ابدأ خطة التقوية";
            }
            else // isStart
            {
                t.Tier = "start";
                t.ThemeCss = "theme-growth";
                t.IconCss = "bi-compass text-primary";

                if (isDiagnostic)
                {
                    t.Title = context == ResultContext.Placement
                        ? "تم تحديد نقطة انطلاقك ✨"
                        : "هذه قراءة أدائك اليوم 📊";
                    t.Subtitle = "هذه النتيجة تقيس نقطة بدايتك فقط. حدّدنا لك المحاور التي ستصنع أكبر فرق — وكل خطوة قادمة ترفع مستواك.";
                    t.BadgeText = "بداية الخطة";
                }
                else
                {
                    t.Title = "بدايتك من هنا 💪";
                    t.Subtitle = "هذه النتيجة نقطة انطلاق وليست حكماً. حدّدنا لك المحاور الأعلى أثراً — وكل محاولة قادمة ترفع رقمك.";
                    t.BadgeText = "قيد التقدّم";
                }

                t.BadgeCss = "er-badge--progress";
                t.MotivationHeading = "خطوتك القادمة أهم من رقم اليوم";
                t.MotivationMsg = "📈 ابدأ بمحور واحد اليوم. الطلاب الذين بدؤوا من هنا رفعوا نتيجتهم بالمحاولة التالية — وأنت مثلهم.";
                t.NextStepLabel = "ابدأ خطة التقوية";
            }

            return t;
        }

        /// <summary>
        /// تسمية مستوى محايدة ومحفّزة لمحور/درس داخل التقارير (بديل كلمة "ضعيف").
        /// </summary>
        public static string SectionLevelLabel(double accuracy)
        {
            if (accuracy >= 85) return "ممتاز";
            if (accuracy >= 70) return "جيد جداً";
            if (accuracy >= 55) return "جيد";
            if (accuracy >= 40) return "قيد التطوير";
            return "محور أولوية";
        }

        /// <summary>تقدير عام محايد للتقارير (بديل "ضعيف").</summary>
        public static string OverallGradeLabel(double percentage)
        {
            if (percentage >= 80) return "ممتاز";
            if (percentage >= 70) return "جيد";
            if (percentage >= 60) return "متوسط";
            if (percentage >= 40) return "قيد التطوير";
            return "محور أولوية";
        }
    }
}
