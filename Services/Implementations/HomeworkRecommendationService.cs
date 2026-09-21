using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;
using System;
using System.Linq;

namespace QdratNew.Services.Implementations
{
    public class HomeworkRecommendationService : IHomeworkRecommendationService
    {
        public HomeworkRecommendationVm GenerateRecommendation(HomeworkAnalyticsVm analytics)
        {
            var vm = new HomeworkRecommendationVm();

            double score = analytics.ScorePercentage;
            double timeSpent = analytics.TimeSpentMinutes;
            double estimatedTime = analytics.TotalQuestions > 0 ? analytics.TotalQuestions * 1.5 : 1; // 1.5 دقيقة للسؤال كحد تقديري
            double percentTime = Math.Round((timeSpent / estimatedTime) * 100, 1);

            // 🧭 التصنيف العام للسرعة
            vm.SpeedLabel = percentTime < 40 ? "حل سريع جدًا"
                           : percentTime < 70 ? "سرعة مناسبة"
                           : "حل بطيء نسبيًا";

            // 🧩 التوصية الزمنية + الدقة
            if (percentTime < 40)
            {
                if (score < 50)
                    vm.TimeNote = "حل سريع جدًا مع ضعف شديد في الدقة — يُحتمل عشوائية في الإجابات.";
                else if (score < 60)
                    vm.TimeNote = "سرعة زائدة مع قلة تركيز — أعد قراءة السؤال قبل الاختيار.";
                else if (score < 70)
                    vm.TimeNote = "حل سريع جيد لكنه يحتاج إلى مراجعة أدق قبل التسليم.";
                else if (score < 80)
                    vm.TimeNote = "حل سريع متوازن — أداء متميز في إدارة الوقت.";
                else
                    vm.TimeNote = "حل احترافي يجمع بين السرعة والدقة — ممتاز جدًا!";
            }
            else if (percentTime < 70)
            {
                if (score < 50)
                    vm.TimeNote = "زمن مناسب لكن الدقة ضعيفة — راجع المفاهيم الأساسية قبل المحاولة التالية.";
                else if (score < 60)
                    vm.TimeNote = "حل متوازن يحتاج إلى زيادة الانتباه للأسئلة التحليلية.";
                else if (score < 70)
                    vm.TimeNote = "حل متوازن جيد — واصل التدريب المنتظم لتحسين الدقة.";
                else if (score < 80)
                    vm.TimeNote = "حل متقن ضمن الوقت المثالي — حافظ على هذا المستوى.";
                else
                    vm.TimeNote = "حل مثالي متوازن — دقة وسرعة ممتازتان.";
            }
            else
            {
                // ⚠️ مهم: التأني في الحل ليس عيبًا بذاته — لا نطلب "التسريع" إلا عندما تكون
                // الدقة ضعيفة فعلًا (الوقت الإضافي لم يُترجَم إلى نتيجة أفضل). أما مع الدقة
                // الجيدة أو الممتازة فالرسالة يجب أن تكون إيجابية وتُقرّ بأن التأني أفاد الطالب.
                if (score < 50)
                    vm.TimeNote = "استغرقت وقتًا طويلًا دون تحقيق نتيجة جيدة — يبدو أن التحدي في فهم المحتوى وليس في الوقت نفسه، راجع المفاهيم الأساسية أولًا.";
                else if (score < 60)
                    vm.TimeNote = "زمن طويل مع دقة أقل من المتوسط — قلل التردد بين الخيارات وركّز على فهم السؤال من أول قراءة.";
                else if (score < 70)
                    vm.TimeNote = "دقة جيدة رغم استغراق وقت أطول من المعتاد — التأني أعطى نتيجة جيدة، استمر مع محاولة تنظيم وقتك بين الأسئلة.";
                else if (score < 80)
                    vm.TimeNote = "دقة عالية مع أسلوب متأنٍ في الحل — هذا مؤشر إيجابي، ولا داعي لتغيير طريقتك ما لم يكن هناك وقت محدد يجب الالتزام به.";
                else
                    vm.TimeNote = "دقة ممتازة مع تأنٍ واضح في الحل — أسلوبك الدقيق هو سبب هذه النتيجة، والتأني هنا ميزة وليس عيبًا.";
            }

            // 🧠 التوصية العامة للأداء
            if (score >= 80)
                vm.PerformanceSummary = "🟢 أداء ممتاز — حافظ على هذا المستوى في الواجبات القادمة.";
            else if (score >= 60)
                vm.PerformanceSummary = "🟡 أداء جيد — تحتاج إلى تحسين الدقة في بعض المؤشرات الضعيفة.";
            else
                vm.PerformanceSummary = "🔴 الأداء يحتاج إلى مراجعة شاملة — ركّز على نقاط الضعف الأساسية.";

            // 💡 الملاحظات الذكية الإضافية
            if (percentTime < 20 && score >= 90)
                vm.LessonTips.Add("⭐ تميّز استثنائي — سرعة تحليلية عالية جدًا مع دقة ممتازة.");
            if (percentTime < 30 && score < 60)
                vm.LessonTips.Add("⚠️ سرعة مفرطة مع دقة منخفضة — يُحتمل إجابات عشوائية.");
            if (percentTime >= 70 && score >= 80)
                vm.LessonTips.Add("🧘 التأني في الحل انعكس إيجابًا على دقتك — استمر بهذا الأسلوب دون ضغط نفسك على السرعة.");

            vm.LessonTips.Add($"⏱️ نسبة الوقت المستهلك: {percentTime}% من الزمن المتوقع.");
            vm.LessonTips.Add("💡 احرص على مراجعة الأسئلة الخاطئة بعد كل واجب لفهم نمط الخطأ وليس فقط النتيجة.");

            // 🟦 تحليل المؤشرات الضعيفة
            var weakLessons = analytics.LessonsPerformance
                .Where(l => l.SuccessRate < 60)
                .OrderBy(l => l.SuccessRate)
                .ToList();

            if (weakLessons.Any())
            {
                foreach (var lesson in weakLessons)
                {
                    var minutes = Math.Max(lesson.TotalQuestions * 4, 10);
                    vm.LessonTips.Add($"📘 راجع المؤشر: «{lesson.LessonTitle}» لمدة تقريبًا {minutes} دقيقة (نسبة النجاح {lesson.SuccessRate}%).");
                }
            }
            else
            {
                vm.LessonTips.Add("🎯 لا توجد مؤشرات ضعيفة — جميع النتائج ضمن المستوى المطلوب.");
            }

            // 🟩 تحليل المحاور
            foreach (var section in analytics.SectionsPerformance.OrderBy(s => s.Accuracy))
            {
                if (section.Accuracy < 60)
                    vm.SectionTips.Add($"🔹 المحور «{section.SectionTitle}» يحتاج مراجعة إضافية (نسبة {section.Accuracy}%). خصص له 30 دقيقة هذا الأسبوع.");
                else if (section.Accuracy >= 80)
                    vm.SectionTips.Add($"✅ المحور «{section.SectionTitle}» متقن بنسبة {section.Accuracy}% — ممتاز!");
            }

            // 🏁 التوصية الختامية
            vm.FinalAdvice = weakLessons.Any()
                ? $"📅 يُنصح بمراجعة {weakLessons.Count} مؤشرات ضعيفة هذا الأسبوع بواقع 15–20 دقيقة لكل منها."
                : "🌟 لا توجد مؤشرات تحتاج مراجعة إضافية — استمر في التدريب المنتظم.";

            return vm;
        }
    }
}
