using System;

namespace QdratNew.Entities
{
    public class HomeworkDraftQuestion
    {
        public int Id { get; set; }

        public int HomeworkDraftId { get; set; }
        public HomeworkDraft HomeworkDraft { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public int LessonId { get; set; }
        public int SectionId { get; set; }

        public int Order { get; set; }
    }
}
