using System;
using System.Collections.Generic;
using QdratNew.Enums;

namespace QdratNew.Services.Exams.Models
{
    // مدخل اختيار مؤشر واحد (Lesson) بصعوبة معينة أثناء توليد أسئلة مرحلة
    public class IndicatorSelectionInput
    {
        public int LessonId { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public int RequestedCount { get; set; }
    }

    // نتيجة تنفيذ GenerateStageQuestionsAsync
    public class MinistrySimExamGenerationResult
    {
        public bool IsSuccess { get; set; }
        // لكل نقص: رسالة عربية دقيقة تحدد المحور/المؤشر/الصعوبة والعدد الناقص بالضبط
        public List<string> ShortfallMessages { get; set; } = new();
    }

    // نتيجة تنفيذ ValidateExactCountsAsync — بوابة الاكتمال الصارمة قبل النشر
    public class MinistrySimExamValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ShortfallMessages { get; set; } = new();
    }

    // Sprint 7 (MSE-D / D4): نتيجة تنفيذ AddQuestionManuallyAsync
    public class MinistrySimExamManualAddResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
