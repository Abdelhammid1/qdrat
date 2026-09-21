using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

public class DropdownService : IDropdownService
{
    private readonly ApplicationDbContext _context;

    public DropdownService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SelectListItem>> GetBranchesAsync()
    {
        return await _context.Branches
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToListAsync();
    }

    public async Task<List<SelectListItem>> GetBatchesAsync()
    {
        return await _context.Batches
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToListAsync();
    }
}
