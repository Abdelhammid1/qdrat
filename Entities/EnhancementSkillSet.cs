namespace QdratNew.Entities
{
    public class EnhancementSkillSet
    {
        public int Id { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        // اختياري — يُستخدم فقط في بعض السياقات القديمة
        public int? LectureId { get; set; }
        public Lecture? Lecture { get; set; }

        public string Title { get; set; } = "مهارات تعزيزية";
        public string? Description { get; set; }

        // 1 = محاور/مؤشرات، 2 = نموذج احترافي
        public int GenerationMethod { get; set; } = 1;

        public int? ProfessionalModelId { get; set; }
        public ProfessionalModel? ProfessionalModel { get; set; }

        // إجمالي الأسئلة لكل طالب (يُحسب تلقائياً من المؤشرات أو يُحدد يدوياً للنموذج)
        public int QuestionsPerStudent { get; set; } = 5;

        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }

        // وقت الإرسال المجدول (اختياري — يرسل تلقائياً في هذا الوقت)
        public DateTime? ScheduledSendAt { get; set; }

        public DateTime? EndAt { get; set; }

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<EnhancementSkillAssignment> Assignments { get; set; } = new List<EnhancementSkillAssignment>();
        public ICollection<EnhancementSetIndicator> Indicators { get; set; } = new List<EnhancementSetIndicator>();
    }
}
