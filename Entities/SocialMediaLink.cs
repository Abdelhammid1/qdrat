namespace QdratNew.Entities
{
    public class SocialMediaLink
    {
        public int Id { get; set; }

        // مثل "Facebook", "Twitter", "YouTube", "TikTok"
        public string Platform { get; set; } = string.Empty;

        // رابط الصفحة
        public string Url { get; set; } = string.Empty;

        // أيقونة FontAwesome مثلاً: "fab fa-facebook"
        public string IconClass { get; set; } = string.Empty;

        // ترتيب الظهور في الواجهة
        public int DisplayOrder { get; set; }

        // هل مفعل؟
        public bool IsActive { get; set; } = true;
    }
}
