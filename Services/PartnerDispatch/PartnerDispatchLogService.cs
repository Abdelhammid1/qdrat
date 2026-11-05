using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.PartnerDispatch
{
    public class PartnerDispatchLogService : IPartnerDispatchLogService
    {
        private const int BulkThreshold = 20;
        private const int TitleMaxLength = 300;

        private readonly ApplicationDbContext _context;

        public PartnerDispatchLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entries);
            if (entries.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var entry in entries)
                Normalize(entry, now);

            if (entries.Count > BulkThreshold)
            {
                await _context.BulkInsertAsync(entries.ToList(), cancellationToken: ct);
                return;
            }

            _context.PartnerDispatchLogs.AddRange(entries);
            await _context.SaveChangesAsync(ct);
        }

        public IQueryable<PartnerDispatchLog> Query() => _context.PartnerDispatchLogs.AsNoTracking();

        private static void Normalize(PartnerDispatchLog entry, DateTime now)
        {
            if (entry.PartnerId <= 0)
                throw new ArgumentException("سجل الإرسال يتطلب شريكًا.", nameof(entry));

            if (entry.DispatchGroupId == Guid.Empty)
                throw new ArgumentException("سجل الإرسال يتطلب معرّف مجموعة الإرسال.", nameof(entry));

            if (!Enum.IsDefined(entry.Kind))
                throw new ArgumentException("نوع الإرسال غير معروف.", nameof(entry));

            if (string.IsNullOrWhiteSpace(entry.SentByUserId))
                throw new ArgumentException("سجل الإرسال يتطلب المرسل.", nameof(entry));

            entry.Title = Truncate(entry.Title?.Trim() ?? "", TitleMaxLength);

            var reason = entry.OverrideReason?.Trim();
            entry.OverrideReason = entry.ContractOverridden && !string.IsNullOrEmpty(reason)
                ? Truncate(reason, DispatchRules.OverrideReasonMaxLength)
                : null;

            if (entry.SentAtUtc == default)
                entry.SentAtUtc = now;
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
