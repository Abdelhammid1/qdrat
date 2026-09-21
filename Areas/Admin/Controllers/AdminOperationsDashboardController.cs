using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.AdminDashboard;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,DataEntry")]
    public class AdminOperationsDashboardController : Controller
    {
        private readonly IAdminOperationsDashboardService _dashboardService;
        private readonly IAdminOperationsDrillDownService _drillDownService;
        private readonly IAdminLiveStudentTracker _liveStudentTracker;

        public AdminOperationsDashboardController(
            IAdminOperationsDashboardService dashboardService,
            IAdminOperationsDrillDownService drillDownService,
            IAdminLiveStudentTracker liveStudentTracker)
        {
            _dashboardService = dashboardService;
            _drillDownService = drillDownService;
            _liveStudentTracker = liveStudentTracker;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _dashboardService.GetDashboardAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Pulse()
        {
            var model = await _dashboardService.GetPulseAsync();
            return Json(model);
        }

        [HttpGet]
        public IActionResult LiveDebug()
        {
            var snapshots = _liveStudentTracker.GetSnapshots();

            return Json(snapshots.Select(item => new
            {
                item.StudentId,
                item.UserId,
                item.CurrentPath,
                item.CurrentPageTitle,
                item.FirstSeenAt,
                item.LastSeenAt,
                item.PageHitCount,
                item.IsLiveNow,
                item.IsMoving
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ActiveStudents()
        {
            var model = await _drillDownService.GetActiveStudentsAsync();
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> RunningLectures()
        {
            var model = await _drillDownService.GetRunningLecturesAsync();
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> OpenExams()
        {
            var model = await _drillDownService.GetOpenExamsAsync();
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> TodayAttendance()
        {
            var model = await _drillDownService.GetTodayAttendanceAsync();
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> ClosingHomeworks()
        {
            var model = await _drillDownService.GetClosingHomeworksAsync();
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> BatchHealth(int? batchId)
        {
            var model = await _drillDownService.GetBatchHealthAsync(batchId);
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> InstructorActivity(int? instructorId)
        {
            var model = await _drillDownService.GetInstructorActivityAsync(instructorId);
            return View("DrillDown", model);
        }

        [HttpGet]
        public async Task<IActionResult> SmartAlerts()
        {
            var model = await _drillDownService.GetSmartAlertsReferenceAsync();
            return View("DrillDown", model);
        }
    }
}
