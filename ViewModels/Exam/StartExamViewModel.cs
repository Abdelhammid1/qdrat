using QdratNew.ViewModels.Question;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class StartExamViewModel
    {
        public int? ExamAssignmentId { get; set; } // يمكن أن يكون null في الاختبار الذاتي
        public string? ExamTitle { get; set; }
        public List<QuestionDisplayViewModel> Questions { get; set; } = new();
    }
}
