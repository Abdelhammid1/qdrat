using QdratNew.Services.PartnerDispatch;

namespace QdratNew.ViewModels.PartnerDispatch
{
    // APD-S6.1: صفحة سجل الإرسال للمدارس الشريكة في الأدمن (Admin/PartnerDispatchLog/Index)
    public sealed class PartnerDispatchLogIndexViewModel
    {
        public PartnerDispatchLogFilter Filter { get; init; } = new();
        public PartnerDispatchLogPage Result { get; init; } = new();
        public PartnerDispatchLogFilterOptions Options { get; init; } = new();
    }
}
