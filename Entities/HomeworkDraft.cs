using System;
using System.Collections.Generic;

namespace QdratNew.Entities
{
    public class HomeworkDraft
    {


        public int Id { get; set; }

        public int PartnerId { get; set; }
        public int SubscriptionPeriodId { get; set; }

        public int CourseId { get; set; }
        public int? SectionId { get; set; }

        public string Title { get; set; }

        public int QuestionsPerLesson { get; set; }

        public bool UsePlatformQuestionBank { get; set; }
        public bool UsePrivateQuestionBank { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsArchived { get; set; } = false;

        public bool IsDeleted { get; set; } = false;

        public DateTime? ArchivedAt { get; set; }

        public ICollection<HomeworkDraftQuestion> Questions { get; set; }
            = new List<HomeworkDraftQuestion>();


    }
}
