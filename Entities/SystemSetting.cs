namespace QdratNew.Entities
{
    public class SystemSetting
    {
        public int Id { get; set; }

        public string Key { get; set; } = null!;

        public string Value { get; set; } = null!;

        public string Description { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ Constructor مخصص لتسهيل الإنشاء
        public SystemSetting(string key, string value, string description)
        {
            Key = key;
            Value = value;
            Description = description;
        }

        // ✅ Constructor فارغ مطلوب لـ EF
        public SystemSetting() { }
    }
}
