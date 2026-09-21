using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UsersTableRowDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }      // من ApplicationUser
        public string Email { get; set; }
        public bool IsActive { get; set; }

        public bool IsStudent { get; set; }       // موجود له Student؟
        public int EnrolledBatchesCount { get; set; } // عدد الدُفعات المنضم لها لو طالب

        public List<string> Roles { get; set; } = new();
    }
}
