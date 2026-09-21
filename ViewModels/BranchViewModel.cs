using System.ComponentModel.DataAnnotations;

namespace MyNamespace
{

public class BranchViewModel
{
    public int Id { get; set; }  // في حال كان تعديل
    [Required(ErrorMessage = "اسم الفرع مطلوب")]
    public string Name { get; set; }

    [Required(ErrorMessage = "الموقع مطلوب")]
    public string Location { get; set; }

    public List<BranchViewModel>? Branches { get; set; } // لجلب جميع الفروع عند عرض النموذج
}
}