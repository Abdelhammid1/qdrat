using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // اختبار محاكاة اختبار الوزارة — Sprint 6 (MSE-D / D1): شاشة مراجعة أسئلة المرحلة بترتيب StageOrder
    public class MinistrySimExamReviewStageVm
    {
        public int StageId { get; set; }
        public int MinistrySimExamId { get; set; }
        public int StageNumber { get; set; }
        public string QuantSectionTitle { get; set; }
        public string VerbalSectionTitle { get; set; }
        public int DurationMinutes { get; set; }

        public List<MinistrySimExamReviewQuestionVm> Questions { get; set; } = new();

        // Sprint 7 (MSE-D / D3-D4): حالة كل مؤشر (مطلوب مقابل فعلي) لعرض أزرار "إعادة توليد"/"إضافة يدوية" عند النقص
        public List<MinistrySimExamReviewIndicatorStatusVm> IndicatorStatuses { get; set; } = new();
    }

    // Sprint 7 (MSE-D / D3-D4): حالة اكتمال مؤشر واحد (Lesson+Difficulty) داخل مرحلة معيّنة
    public class MinistrySimExamReviewIndicatorStatusVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public bool IsQuant { get; set; }
        public int DifficultyLevel { get; set; }
        public int RequestedCount { get; set; }
        public int ActualCount { get; set; }
        public bool IsComplete => ActualCount == RequestedCount;
    }

    public class MinistrySimExamReviewQuestionVm
    {
        public int StageQuestionId { get; set; }
        public Guid QuestionId { get; set; }
        public int StageOrder { get; set; }
        public int GlobalOrder { get; set; }
        public bool IsQuant { get; set; }
        public bool IsManuallySelected { get; set; }
        public string Title { get; set; }
        public string CorrectAnswer { get; set; }
        public int DifficultyLevel { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
    }

    // Sprint 6 (MSE-D / D2): شاشة استبدال سؤال — بدائل من نفس المؤشر (Lesson) ونفس الصعوبة بالضبط
    public class MinistrySimExamReplaceQuestionVm
    {
        public int StageQuestionId { get; set; }
        public int MinistrySimExamId { get; set; }
        public int StageId { get; set; }

        public Guid OldQuestionId { get; set; }
        public string OldTitle { get; set; }
        public int StageOrder { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public int DifficultyLevel { get; set; }

        public List<MinistrySimExamAlternativeQuestionVm> Alternatives { get; set; } = new();
    }

    public class MinistrySimExamAlternativeQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string CorrectAnswer { get; set; }
        public int DifficultyLevel { get; set; }
    }

    // Sprint 7 (MSE-D / D4): شاشة اختيار سؤال لإضافته يدويًا لمؤشر ناقص ضمن مرحلة — مرشحون من نفس Lesson+Difficulty فقط
    public class MinistrySimExamAddQuestionVm
    {
        public int StageId { get; set; }
        public int MinistrySimExamId { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public int DifficultyLevel { get; set; }

        public List<MinistrySimExamAlternativeQuestionVm> Candidates { get; set; } = new();
    }
}
