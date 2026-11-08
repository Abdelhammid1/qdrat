using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Security;
using QdratNew.Services.PartnerDispatch;
using QdratNew.ViewModels.PartnerDispatch;

namespace QdratNew.Areas.Admin.Controllers
{
    // APD-S6.1: سجل الإرسال للمدارس الشريكة — قراءة وتصدير فقط بصلاحية PartnerDispatch.ReadLog.
    // لا روابط لنتائج الطلاب: ما أُرسل للمدرسة ملكها ويُتابَع من منطقتها.
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class PartnerDispatchLogController : Controller
    {
        private readonly IPartnerDispatchLogReportService _report;

        public PartnerDispatchLogController(IPartnerDispatchLogReportService report)
        {
            _report = report;
        }

        [HttpGet]
        [AdminPermission("PartnerDispatch", "ReadLog")]
        public async Task<IActionResult> Index([FromQuery] PartnerDispatchLogFilter filter, CancellationToken ct)
        {
            filter ??= new PartnerDispatchLogFilter();

            var vm = new PartnerDispatchLogIndexViewModel
            {
                Filter = filter,
                Result = await _report.SearchAsync(filter, ct),
                Options = await _report.GetFilterOptionsAsync(ct)
            };

            return View(vm);
        }

        [HttpGet]
        [AdminPermission("PartnerDispatch", "ReadLog")]
        public async Task<IActionResult> Export([FromQuery] PartnerDispatchLogFilter filter, CancellationToken ct)
        {
            var export = await _report.ExportAsync(filter ?? new PartnerDispatchLogFilter(), ct);

            return File(
                export.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                export.FileName);
        }
    }
}
