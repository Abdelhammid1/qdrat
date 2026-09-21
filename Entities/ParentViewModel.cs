namespace QdratNew.Entities
{
    public class ParentViewModel
    {
        public int ParentID { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public int StudentCount { get; set; } // ✅ عدد الطلاب التابعين لولي الأمر
    }
}
