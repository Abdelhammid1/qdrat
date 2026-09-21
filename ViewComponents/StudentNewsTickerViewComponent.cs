using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using QdratNew.ViewModels.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using QdratNew.Entities;

public class StudentNewsTickerViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentNewsTickerViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null) return View("Empty");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (student == null) return View("Empty");

        var notifications = await _context.Notifications
            .Where(n => n.StudentID == student.StudentID && !n.IsRead)
            .OrderByDescending(n => n.SentAt)
            .Select(n => n.Message)
            .Take(10)
            .ToListAsync();

        var model = new StudentNewsTickerViewModel
        {
            Notifications = notifications
        };

        return View("_NewsTickerPartial", model);
    }
}
