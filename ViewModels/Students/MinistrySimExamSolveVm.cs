using System;
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Students
{
    // Sprint 9 (MSE-F / F2): شاشة حل الأسئلة — سؤال واحد من مرحلة اختبار معمل القياس الحالية في كل مرة
    public class MinistrySimExamSolveVm
    {
        public int MinistrySimExamId { get; set; }
        public int StageNumber { get; set; }

        public int StageQuestionId { get; set; }
        public Guid CurrentQuestionId { get; set; }

        public int CurrentIndex { get; set; } // StageOrder — 1..24
        public int Total { get; set; }

        public QuestionDisplayViewModel Question { get; set; }

        public int? SelectedOptionIndex { get; set; }
        public bool IsFlaggedForReview { get; set; }

        public List<MinistrySimExamStageNavigationVm> ReviewQuestions { get; set; } = new();
        public bool IsReviewVisible { get; set; }

        // Sprint 10 (F3): الوقت المتبقي بالثواني عند لحظة العرض — للعرض فقط، الحسم النهائي دائمًا على الخادم
        public int RemainingSeconds { get; set; }
    }

    public class MinistrySimExamStageNavigationVm
    {
        public Guid QuestionId { get; set; }
        public int Number { get; set; }
        public bool IsAnswered { get; set; }
        public bool IsFlaggedForReview { get; set; }
    }
}
