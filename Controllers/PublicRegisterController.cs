using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.ViewModels.Frontend;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Controllers
{
    public class PublicRegisterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PublicRegisterController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // 📌 فورم تسجيل عام – Static
        // /register
        // ===============================
        [HttpGet("/register")]
        public IActionResult Index()
        {
            var vm = new CourseLeadFormVM
            {
                Programs = GetStaticPrograms()
            };

            return View(vm);
        }

        // ===============================
        // 📩 حفظ البيانات
        // ===============================
        [HttpPost("/register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(CourseLeadFormVM model)
        {
            if (!ModelState.IsValid)
            {
                model.Programs = GetStaticPrograms();
                return View("Index", model);
            }

            var lead = new FrontendLead
            {
                StudentName = model.StudentName,
                PhoneNumber = model.PhoneNumber,
                SelectedProgram = model.SelectedProgram
            };

            _context.FrontendLeads.Add(lead);
            await _context.SaveChangesAsync();

            return View("Success");
        }

        // ===============================
        // 📋 البرامج الثابتة
        // ===============================
        private List<SelectListItem> GetStaticPrograms()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "القدرات التأسيسية العامة", Text = "القدرات التأسيسية العامة" },
                new SelectListItem { Value = "النماذج الاحترافية للقدرات", Text = "النماذج الاحترافية للقدرات" },
                new SelectListItem { Value = "برنامج القدرات الشامل (تأسيس + نماذج)", Text = "برنامج القدرات الشامل (تأسيس + نماذج)" },
                new SelectListItem { Value = "التحصيلي", Text = "التحصيلي" },
                new SelectListItem { Value = "STEP", Text = "STEP" },
                new SelectListItem { Value = "اللغة الإنجليزية (تمهيدي)", Text = "اللغة الإنجليزية (تمهيدي)" },
                new SelectListItem { Value = "اللغة الإنجليزية (مستويات أكسفورد)", Text = "اللغة الإنجليزية (مستويات أكسفورد)" }
            };
        }
    }
}
