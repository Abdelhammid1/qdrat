using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities.Frontend
{
    public class FrontendCourseRegistration
    {
        public int Id { get; set; }

        [ForeignKey("FrontendCourse")]
        public int FrontendCourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required]
        [MaxLength(200)]
        public string Email { get; set; }

        [Required]
        [MaxLength(20)]
        public string NationalId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; }

        [Required]
        [MaxLength(100)]
        public string Password { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
        public virtual FrontendCourse FrontendCourse { get; set; }
        public int SubCourseId { get; internal set; }



     

     
        [ForeignKey(nameof(SubCourseId))]
        public SubCourse SubCourse { get; set; }



        public bool IsContacted { get; set; } = false;  // ⏳ لم يتم التواصل عند الإنشاء


    }
}
