using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class StartHomeworkDraftVM
    {
        public List<SelectListItem> Courses { get; set; } = new();
    }
}
