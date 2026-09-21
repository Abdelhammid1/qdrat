using System;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamDraftListVM
    {
        public int DraftId { get; set; }
        public string Title { get; set; }
        public string CurriculumTitle { get; set; }
        public int QuestionsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
