using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Parent
{
    public class ParentIndexViewModel
    {
        public List<ParentRowViewModel> Parents { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? RelationFilter { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class ParentRowViewModel
    {
        public int ParentID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string RelationToStudent { get; set; } = string.Empty;
        public string NationalID { get; set; } = string.Empty;
        public string? WhatsAppNumber { get; set; }
        public DateTime DateCreated { get; set; }
        public int StudentsCount { get; set; }
        public bool HasUserAccount { get; set; }
    }
}
