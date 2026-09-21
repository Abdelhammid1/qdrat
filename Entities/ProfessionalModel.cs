using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class ProfessionalModel
    {
        public int Id { get; set; }
        public string Title { get; set; } // مثال: M75
        [Required(ErrorMessage = "الوصف مطلوب")]

        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        public bool IsGlobal { get; set; } = false;

        public int? CurriculumId { get; set; }
        public Curriculum? Curriculum { get; set; }

        public ProfessionalModelType ModelType { get; set; } = ProfessionalModelType.Exam;

        public ICollection<ProfessionalModelPartner> Partners { get; set; }
            = new List<ProfessionalModelPartner>();

        public ICollection<ProfessionalModelSubscriptionPeriod> SubscriptionPeriods { get; set; }
            = new List<ProfessionalModelSubscriptionPeriod>();

        public ICollection<ProfessionalModelQuestion> Questions { get; set; }
    }
}
