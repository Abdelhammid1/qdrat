using System;

namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class QuestionCandidateVM
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public int LessonId { get; set; }

        public string LessonTitle { get; set; }

        public int? SectionId { get; set; }

        public string SectionTitle { get; set; }

        public bool IsQuantitative { get; set; }

        public string Difficulty { get; set; }
        public Guid QuestionId { get; internal set; }
    }
}