using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialVideo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Lesson")]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        [Required, StringLength(300)]
        public string Title { get; set; }

        [Required, StringLength(500)]
        public string VimeoUrl { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // 🔗 ربط الأسئلة المتعلقة بالفيديو
        public string? ThumbnailUrl { get; set; }


        public ICollection<RemedialVideoQuestion> Questions { get; set; } = new List<RemedialVideoQuestion>();
    }

}
