using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class ExamQuestionsListViewModel
    {
        public string ExamTitle { get; set; }

        public int AssignmentId { get; set; }
        public List<ExamQuestionViewModel> Questions { get; set; }
    }
}
