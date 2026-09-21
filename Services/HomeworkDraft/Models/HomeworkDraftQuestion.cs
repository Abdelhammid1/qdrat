using System;

namespace QdratNew.Services.HomeworkDraft.Models
{
    public class HomeworkDraftQuestion
    {
        public Guid QuestionId { get; set; }
        public int LessonId { get; set; }
        public int SectionId { get; set; }
        public int Order { get; set; }
    }
}
