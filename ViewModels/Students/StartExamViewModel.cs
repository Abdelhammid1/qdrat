using System;
using System.Collections.Generic;
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Students
{
    public class StartExamViewModel
    {
        public int ExamAssignmentId { get; set; }

        public string ExamTitle { get; set; }

        public List<QuestionDisplayViewModel> Questions { get; set; } = new();

        // ✅ للإجابات المؤقتة قبل الإرسال النهائي
        public Dictionary<Guid, string> StudentAnswers { get; set; } = new();



    }
}
