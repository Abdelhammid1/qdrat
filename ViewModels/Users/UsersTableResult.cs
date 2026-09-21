using QdratNew.ViewModels.Users;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UsersTableResult
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<UsersTableRowDto> Items { get; set; } = new();
    }
}
