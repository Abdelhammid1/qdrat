using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UsersIndexViewModel
    {
        // فلاتر
        public string RoleFilter { get; set; }          // "All" | "Admin" | "Student" | ...
        public bool? IsActive { get; set; }             // null=الكل، true/false
        public bool? IsStudent { get; set; }            // null=الكل

        // DropDowns
        public IEnumerable<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> ActiveStates { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> StudentStates { get; set; } = new List<SelectListItem>();
        // فلاتر
  
        public string SearchText { get; set; } = "";

        // بيانات الجدول
        public List<UserRowDto> Users { get; set; } = new();

        // ترقيم
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public int PagesCount => (RecordsFiltered + PageSize - 1) / PageSize;
    }

    public class UserRowDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string NationalID { get; set; }

        public string Email { get; set; }
        public bool IsStudent { get; set; }
        public int EnrolledBatchesCount { get; set; }
        public bool IsActive { get; set; }
        public long LastLoginAtTicks { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> BatchNames { get; set; } = new();
    }
}
