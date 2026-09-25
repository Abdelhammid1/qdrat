using System.Threading;
using System.Threading.Tasks;
using QdratNew.ViewModels.Admin.FrontendLeads;

namespace QdratNew.Services.Frontend.Leads;

// RL-S5 — خدمة لوحة طلبات الالتحاق (إحصائيات + قائمة + تحديث الحالة)
public interface IFrontendLeadAdminService
{
    Task<FrontendLeadsDashboardVM> GetDashboardAsync(CancellationToken ct = default);

    Task<FrontendLeadCountsVM> GetCountsAsync(CancellationToken ct = default);

    Task<UpdateLeadStatusResult> UpdateStatusAsync(UpdateLeadStatusRequest req, string? userId, CancellationToken ct = default);

    // للتوافق مع MarkContacted القديم: يحوّل الطلب إلى «تم التواصل» دون المساس بالملاحظات
    Task<bool> MarkContactedAsync(int id, string? userId, CancellationToken ct = default);
}
