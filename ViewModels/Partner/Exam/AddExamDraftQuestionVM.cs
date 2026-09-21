using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class AddExamDraftQuestionVM
    {
        public int DraftId { get; set; }

        public int LessonId { get; set; }

        public string LessonTitle { get; set; } = string.Empty;

        public List<AddExamDraftQuestionItemVM> Questions { get; set; }
            = new List<AddExamDraftQuestionItemVM>();
    }
}
