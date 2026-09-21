using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    }
}
