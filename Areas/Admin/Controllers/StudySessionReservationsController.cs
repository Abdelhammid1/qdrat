using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.StudySessions;


namespace QdratNew
{

    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudySessionReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudySessionReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var reservations = await _context.StudySessionReservations
                .Include(r => r.Student)
                .Include(r => r.Branch)
                .Include(r => r.Instructor)
                .Select(r => new StudySessionReservationViewModel
                {
                    Id = r.Id,
                    StudentName = r.Student.FullName,
                    BranchName = r.Branch.Name,
                    InstructorName = r.Instructor != null ? r.Instructor.FullName : "—",
                    RequestedDate = r.RequestedDate,
                    StartTime = r.StartTime.ToString(@"hh\:mm"),
                    EndTime = r.EndTime.ToString(@"hh\:mm"),
                    Status = r.Status.ToString(),
                    Fee = r.Fee,
                    AdditionalServices = r.AdditionalServices
                })
                .ToListAsync();

            return View(reservations);
        }
    }

}
