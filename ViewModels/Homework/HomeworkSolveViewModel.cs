using QdratNew.ViewModels.Question;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkSolveViewModel
    {
        public string? ErrorMessage { get; set; }

        public int HomeworkSetId { get; set; }
        public string? VerbalPassageContent { get; set; }

        public Guid CurrentQuestionId { get; set; }

        public List<Guid> AllQuestionIds { get; set; } = new();

        public QuestionDisplayViewModel? Question { get; set; }

        public string? SelectedAnswer { get; set; }

        // ✅ عدد السؤال الحالي
        public int CurrentIndex => AllQuestionIds.IndexOf(CurrentQuestionId) + 1;

        // ✅ إجمالي الأسئلة
        public int Total => AllQuestionIds.Count;

        // ✅ إجابات الطالب لكل سؤال (لأغراض التلوين لاحقًا)
        public Dictionary<Guid, string?> AnswersMap { get; set; } = new();

        // ✅ الأسئلة التي تم وضع علامة للمراجعة عليها
        public List<Guid> ReviewMarkedIds { get; set; } = new();
        public bool ForceReviewVisible { get; set; } = false;

        public bool IsHomeworkSubmitted { get; set; }

        public bool IsQuantitative { get; set; }
        public bool UseIndicNumbers { get; set; }

        public bool IsReviewMode { get; set; } = false;
        public Guid? SelectedAnswerId { get; internal set; }
    }
}
