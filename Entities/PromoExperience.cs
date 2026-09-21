using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class PromoExperience
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? MediaUrl { get; set; } // صورة أو فيديو ترويجي

        public bool IsActive { get; set; } = true;
        public int Order { get; set; } = 1;
    }
}
