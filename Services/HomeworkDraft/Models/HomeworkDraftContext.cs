using System.Collections.Generic;

namespace QdratNew.Services.HomeworkDraft.Models
{
    public class HomeworkDraftContext
    {
        // سياق الشريك
        public int PartnerId { get; set; }
        public int SubscriptionPeriodId { get; set; }

        // سياق أكاديمي
        public int CourseId { get; set; }
        public int BatchId { get; set; }

        // نمط التوليد
        public HomeworkGenerationMode Mode { get; set; }

        // توليد تلقائي
        public List<int> LessonIds { get; set; } = new();
        public int? SectionId { get; set; }
        public int QuestionsPerLesson { get; set; }

        // نموذج احترافي
        public int? ProfessionalModelId { get; set; }

        // الأسئلة الناتجة
        public List<HomeworkDraftQuestion> Questions { get; set; } = new();

        // بيانات إنشائية
        public string GeneratedBy { get; set; } = "";
        public int GeneratedByUserId { get; set; }
    }
}
