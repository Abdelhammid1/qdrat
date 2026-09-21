using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Question
{
    public class ManualQuestionSelectionViewModel
    {
        public int LessonId { get; set; }
        public int SectionId { get; set; }
        public int CurriculumId { get; set; }
        public int BatchId { get; set; }

        public string LessonTitle { get; set; } = string.Empty;

        public List<QuestionListViewModel> Questions { get; set; } = new();
        public List<Guid> SelectedQuestionIds { get; set; } = new();
    }
}
