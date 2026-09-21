using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class StaticPage
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required, StringLength(100)]
        public string Slug { get; set; } // مثل: about, privacy, terms, contact

        public bool IsActive { get; set; } = true;
        public string? ImagePath { get; set; } // ✅ مسار الصورة (اختياري)

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
