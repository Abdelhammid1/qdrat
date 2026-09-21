using System;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ExamDraftQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string? QuestionTitle { get; internal set; }
    }
}