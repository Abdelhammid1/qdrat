using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Enums;

namespace QdratNew.Entities.Frontend
{
    public class FrontendLead
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string StudentName { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; }

        // 🔗 الدورة الرئيسية
        public int? FrontendCourseId { get; set; }
        public FrontendCourse? FrontendCourse { get; set; }


        // 🔗 الدورة الفرعية (اختياري)
        public int? SubCourseId { get; set; }
        public SubCourse? SubCourse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string SelectedProgram { get; set; }

        public bool IsContacted { get; set; } = false;

        // ===== RL: بيانات موسّعة =====
        public LeadApplicantType ApplicantType { get; set; } = LeadApplicantType.Student;

        [MaxLength(150)]
        public string? ParentName { get; set; }

        [MaxLength(20)]
        public string? ParentPhone { get; set; }

        public GenderType? Gender { get; set; }

        [MaxLength(50)]
        public string? SchoolStage { get; set; }         // أول/ثاني/ثالث ثانوي/خريج

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // ===== RL: متابعة الأدمن =====
        public FrontendLeadStatus Status { get; set; } = FrontendLeadStatus.New;
        public DateTime? ContactedAt { get; set; }

        [MaxLength(450)]
        public string? ContactedByUserId { get; set; }

        [MaxLength(1000)]
        public string? AdminNotes { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        [MaxLength(45)]
        public string? SourceIp { get; set; }

        public ICollection<FrontendLeadCourse> SelectedCourses { get; set; } = new List<FrontendLeadCourse>();
    }
}
