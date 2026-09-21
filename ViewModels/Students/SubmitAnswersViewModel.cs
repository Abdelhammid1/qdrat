using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class SubmitAnswersViewModel
    {
        public int ExamAssignmentId { get; set; }
        public Dictionary<string, string?> Answers { get; set; } = new();
    }
}
