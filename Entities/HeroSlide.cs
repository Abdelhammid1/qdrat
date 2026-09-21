using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class HeroSlide
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [StringLength(300)]
        public string Description { get; set; }

        [StringLength(200)]
        public string ButtonText { get; set; }

        [StringLength(300)]
        public string ButtonUrl { get; set; }

        public bool ShowButton { get; set; } = true;

        [StringLength(300)]
        public string ImagePath { get; set; } // مسار الصورة داخل wwwroot

        [StringLength(20)]
        public string Alignment { get; set; } = "align-right"; // أو align-center, align-left

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
