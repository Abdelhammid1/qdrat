using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers
{
    public class MessagesController : ParentBaseController
    {
        private readonly IParentMessageService _messageService;

        public MessagesController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IParentMessageService messageService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _messageService = messageService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            var userId = _userManager.GetUserId(User)!;
            var vm = await _messageService.GetMessagesAsync(userId, studentId ?? SelectedStudentId);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? studentId)
        {
            using var db = _contextFactory.CreateDbContext();
            var children = await db.Students
                .AsNoTracking()
                .Where(s => s.ParentId == ParentId)
                .Select(s => new ParentChildCardViewModel
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName
                })
                .ToListAsync();

            var vm = new ParentMessageCreateViewModel
            {
                AvailableStudents = children,
                StudentId = studentId ?? SelectedStudentId ?? 0
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ParentMessageCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                using var db = _contextFactory.CreateDbContext();
                model.AvailableStudents = await db.Students
                    .AsNoTracking()
                    .Where(s => s.ParentId == ParentId)
                    .Select(s => new ParentChildCardViewModel { StudentId = s.StudentID, StudentName = s.FullName })
                    .ToListAsync();
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            await _messageService.CreateMessageAsync(userId, model);

            TempData["SuccessMessage"] = "تم إرسال رسالتك بنجاح. سيتواصل معك فريق المعهد قريبًا.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _messageService.GetMessageDetailsAsync(id, ParentId);
            if (vm == null) return NotFound();
            return View(vm);
        }
    }
}
