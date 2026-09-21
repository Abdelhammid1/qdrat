using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkReviewViewModel
    {

        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int HomeworkSetId { get; set; }
        public List<ReviewedHomeworkQuestionViewModel> Questions { get; set; }
    }

    public class ReviewedHomeworkQuestionViewModel
    {
        public int HomeworkId { get; set; }
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string? Answer { get; set; }
   

        public bool? IsCorrect { get; set; }
    }
}
