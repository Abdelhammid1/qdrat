using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class LessonResource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Lesson")]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } // مثال: "فيديو شرح النسبة والتناسب"

        [Required]
        [Url]
        public string VideoUrl { get; set; }

        public string Description { get; set; } // ملاحظات أو وصف

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
