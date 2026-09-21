using QdratNew.Services.Analytics.Interfaces;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics.Implementation
{
    public class RemedialEngine : IRemedialEngine
    {
        private readonly IWeakPointEngine _weakPointEngine;

        public RemedialEngine(IWeakPointEngine weakPointEngine)
        {
            _weakPointEngine = weakPointEngine;
        }

        public async Task<List<RemedialActionVM>> GeneratePlanAsync(WeakPointFilter filter)
        {
            // ===============================
            // 1️⃣ جلب الدروس الضعيفة
            // ===============================
            var weakLessons = await _weakPointEngine.GetWeakLessonsAsync(filter);

            var actions = new List<RemedialActionVM>();

            foreach (var lesson in weakLessons)
            {
                string actionType;
                string priority;
                string reason;

                // ===============================
                // 2️⃣ تحديد نوع الإجراء
                // ===============================
                if (lesson.WrongPercentage >= 75)
                {
                    actionType = "ReExplain + Homework + Quiz";
                    priority = "High";
                    reason = "نسبة الخطأ مرتفعة جدًا";
                }
                else if (lesson.WrongPercentage >= 50)
                {
                    actionType = "Homework + Quiz";
                    priority = "Medium";
                    reason = "الطلاب يحتاجون تدريب إضافي";
                }
                else if (lesson.WrongPercentage >= 30)
                {
                    actionType = "Quiz";
                    priority = "Low";
                    reason = "تحسين بسيط مطلوب";
                }
                else
                {
                    continue; // تجاهل الدروس القوية
                }

                actions.Add(new RemedialActionVM
                {
                    LessonId = lesson.LessonId,
                    LessonName = lesson.LessonName,
                    WeakPercentage = lesson.WrongPercentage,
                    ActionType = actionType,
                    Priority = priority,
                    Reason = reason
                });
            }

            return actions
                .OrderByDescending(x => x.WeakPercentage)
                .ToList();
        }
    }
}