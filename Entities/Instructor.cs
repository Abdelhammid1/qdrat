using QdratNew.Entities;
using QdratNew.Entities;
using QdratNew.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Instructor
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string NationalID { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        public string Specialization { get; set; }

        [Required]
        public GenderType Gender { get; set; }  // ✅ Enum بدل string

        public bool IsActive { get; set; } = true;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }


        // ===============================
        // 🔵 NEW: Partner Isolation
        // ===============================

        public int? PartnerId { get; set; }

        [ForeignKey(nameof(PartnerId))]
        public Partner? Partner { get; set; }

        public bool IsPartnerInstructor { get; set; } = false;

        public int? PartnerSubscriptionId { get; set; }

        public PartnerSubscription? PartnerSubscription { get; set; }



        // ===============================

        // ===============================
        // 🔴 Soft Delete
        // ===============================

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // ===============================

      


        public ICollection<CourseInstructor> CourseInstructors { get; set; } = new List<CourseInstructor>();
    }

    
  


}
