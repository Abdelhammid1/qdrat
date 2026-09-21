using System.Collections.Generic;
using QdratNew.Entities;

namespace QdratNew.Services
{
    public static class QuestionChangeTracker
    {
        public static string GetChangesSummary(Question oldQ, Question newQ)
        {
            var changes = new List<string>();

            if (oldQ.Title != newQ.Title)
                changes.Add("عنوان السؤال");

            if (oldQ.CorrectAnswer != newQ.CorrectAnswer)
                changes.Add("الإجابة الصحيحة");

            if (oldQ.Explanation != newQ.Explanation)
                changes.Add("الشرح");

            if (oldQ.Template != newQ.Template)
                changes.Add("نمط السؤال");

            if (oldQ.Difficulty != newQ.Difficulty)
                changes.Add("مستوى الصعوبة");

            if (oldQ.VideoUrl != newQ.VideoUrl)
                changes.Add("رابط الفيديو");

            if (oldQ.Options.Count != newQ.Options.Count)
                changes.Add("عدد الخيارات");

            // يمكنك التوسعة حسب الحقول المهمة لديك

            return changes.Count > 0
                ? string.Join("، ", changes)
                : "لم يتم تعديل أي بيانات مهمة.";
        }
    }
}
