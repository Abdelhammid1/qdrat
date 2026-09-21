namespace QdratNew.ViewModels.Users
{
    public class UsersTableQuery
    {
        public int Page { get; set; } = 1;          // 1-based
        public int PageSize { get; set; } = 10;
        public string Search { get; set; }          // بحث نصّي بسيط (FullName/Email)
        public string RoleFilter { get; set; }      // null أو اسم الدور
        public bool? IsActive { get; set; }
        public bool? IsStudent { get; set; }
    }
}
