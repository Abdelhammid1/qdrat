namespace QdratNew.Services.PartnerDispatch
{
    // APD-S6.1: قراءة سجل الإرسال للمدارس الشريكة في الأدمن (عرض وتصدير فقط، بلا روابط لنتائج الطلاب)
    public interface IPartnerDispatchLogReportService
    {
        public const int PageSize = 20;
        public const int MaxExportRows = 10_000; // APD-S6.2: حد للذاكرة على سيرفر واحد

        Task<PartnerDispatchLogPage> SearchAsync(PartnerDispatchLogFilter filter, CancellationToken ct = default);

        Task<PartnerDispatchLogFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default);

        Task<PartnerDispatchLogExport> ExportAsync(PartnerDispatchLogFilter filter, CancellationToken ct = default);
    }
}
