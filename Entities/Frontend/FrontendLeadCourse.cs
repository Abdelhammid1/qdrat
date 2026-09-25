using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend
{
    public class FrontendLeadCourse
    {
        public int Id { get; set; }

        public int FrontendLeadId { get; set; }
        public FrontendLead? FrontendLead { get; set; }

        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        public int? ProjectId { get; set; }

        [Required, MaxLength(200)]
        public string CourseNameSnapshot { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ProjectNameSnapshot { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
