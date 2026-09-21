using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IDropdownService
{
    Task<List<SelectListItem>> GetBranchesAsync();
    Task<List<SelectListItem>> GetBatchesAsync();
}
