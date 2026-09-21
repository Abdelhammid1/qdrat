using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class StaticPageContent
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string PageKey { get; set; } // مثال: About, PrivacyPolicy, Terms, Contact

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string ContentHtml { get; set; } // النص بصيغة HTML لسهولة العرض

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
