using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels;

namespace QdratNew.ViewModels
{

    public class UnitIndexViewModel
    {
        public List<UnitWithSectionsViewModel> Units { get; set; } = new();
    }

    public class UnitWithSectionsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public List<string> SectionTitles { get; set; } = new();
    }
}
