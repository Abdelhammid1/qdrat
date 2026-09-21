using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.HomeworkGeneration
{
    public class HomeworkGenerationDraftViewModel
    {
        public int DraftId { get; set; }

        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;

        public DateTime HomeworkDate { get; set; }
        public TimeSpan? AvailableFromTime { get; set; }

        public int TotalQuestionsCount { get; set; }

        public List<HomeworkDraftLessonViewModel> Lessons { get; set; } = new();
    }
}
