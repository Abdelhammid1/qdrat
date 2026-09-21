using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementResultViewModel
    {
        public int SetId { get; set; }
        public string Title { get; set; } = "";

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double ScorePercentage { get; set; }

        public bool IsQuantitative { get; set; }
        public bool IsRTL { get; set; } = true;

        public List<EnhancementResultItemViewModel> Questions { get; set; } = new();

        // 🟢 خاصية محسوبة تعرض فقط الأسئلة الخاطئة
        public List<EnhancementResultItemViewModel> WrongQuestions =>
            Questions.Where(q => !q.IsCorrect).ToList();
    }

    public class EnhancementResultItemViewModel
    {
        public string QuestionTitle { get; set; } = string.Empty;
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }

        // لو عندك فيديو توضيحي تضيفه هنا
        public string? VideoUrl { get; set; }
    }
}
