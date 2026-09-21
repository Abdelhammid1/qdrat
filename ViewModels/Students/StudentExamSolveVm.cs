using System.Collections.Generic;
using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Students
{
    public class StudentExamSolveVm
    {
        public int ExamAssignmentId { get; set; }

        // ❗ الأسئلة في النظام GUID
        public Guid QuestionId { get; set; }

        public string QuestionTitle { get; set; } = string.Empty;

        public bool IsQuantitative { get; set; }

        public string? VerbalPassage { get; set; }

        // ✅ نستخدم Homework.QuestionOptionVm
        public List<QuestionOptionVm> Options { get; set; } = new();

        public string? SelectedAnswer { get; set; }
        public bool IsMarkedForReview { get; set; }

        // Navigation
        public int CurrentIndex { get; set; }
        public int TotalQuestions { get; set; }

        public bool HasNext { get; set; }
        public bool HasPrev { get; set; }

        public Guid? NextQuestionId { get; set; }
        public Guid? PrevQuestionId { get; set; }
    }
}
