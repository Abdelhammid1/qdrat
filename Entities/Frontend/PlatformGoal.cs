using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend
{
    public class PlatformGoal
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        // 🟡 الأيقونة — نخزن اسم الأيقونة فقط مثل "bi bi-shield-check"
        [MaxLength(100)]
        public string IconClass { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }

}
