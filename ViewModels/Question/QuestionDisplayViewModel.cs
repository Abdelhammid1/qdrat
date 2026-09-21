using QdratNew.Enums;
using QdratNew.Entities;
using System;

namespace QdratNew.ViewModels.Question
{
    public class QuestionDisplayViewModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsAnswerConfirmed { get; set; }
        public string? Hint { get; set; }

        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }

        public string? CorrectAnswer { get; set; } // الإجابة الصحيحة نصاً

        public bool IsQuantitative { get; set; } // السؤال كمي أو لفظي

        public QuestionTemplate Template { get; set; }
        public DifficultyLevel Difficulty { get; set; }

        public List<QuestionOptionDisplayViewModel> Options { get; set; } = new();

        // إذا السؤال من نوع مقارنة
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }

        public QuestionDisplayType DisplayType { get; set; }

        // لمعرفة الـ index للإجابة الصحيحة
        public int? SelectedCorrectIndex { get; set; }

        // للإجابات السابقة إن وجدت
        public string? SelectedAnswer { get; set; }

        // للتوافق مع أنظمة أخرى (تكرار الـ Id لتأكيد)
        public Guid QuestionId { get; set; }

        // في حالة الحل / المراجعة
        public DateTime? AnsweredAt { get; set; }
        public int? TimeSpentSeconds { get; set; }
        public string? VerbalPassageTitle { get; set; }
        public string? VerbalPassageContent { get; set; }
        // =====================================
        // ✅ NEW: التحكم في اتجاه العرض
        // =====================================
        public bool IsRTL { get; set; } = true;
        public string? LessonTitle { get; set; }   // المؤشر (الدرس)
        public string? SectionTitle { get; set; }  // المحور

        // ✅ الخصائص الجديدة المطلوبة للعرض في نتائج الاختبار
        public string? QuestionText { get; set; }        // نص السؤال للعرض
        public string? StudentAnswer { get; set; }       // إجابة الطالب
        public bool IsCorrect { get; set; }              // هل الإجابة صحيحة؟
        public double TimeTakenSeconds { get; set; }     // الوقت المستغرق في الحل
        public int? VerbalPassageDurationSeconds { get; set; }
        public string? VerbalPassageMediaUrl { get; set; }
        public PassageType? VerbalPassageType { get; set; }
        public bool VerbalPassageRequireFullListen { get; set; }
        public int? PassageStartSeconds { get; set; }
        public int? PassageEndSeconds { get; set; }
        public int? VerbalPassageDuration { get; internal set; }

    


    }

    public class QuestionOptionDisplayViewModel
    {
        public string? Text { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsSelected { get; set; } = false;

    }
}
