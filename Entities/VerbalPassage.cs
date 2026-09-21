using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class VerbalPassage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "النص الكامل مطلوب")]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();



        // =========================
        // 🔹 الجديد (Media Support)
        // =========================

        public PassageType Type { get; set; } = PassageType.Text;

        public string? MediaUrl { get; set; }

        public int? DurationSeconds { get; set; }

        public bool RequireFullListen { get; set; } = false;



    }
}
