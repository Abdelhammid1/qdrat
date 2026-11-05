using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.ProfessionalModel;

namespace QdratNew.Services.ProfessionalModels
{
    public class ProfessionalModelAvailabilityService : IProfessionalModelAvailabilityService
    {
        // مفتاح تعطيل وصول الشركاء للنماذج: فقط القيمة الصريحة false تعطّله، والأثر فوري بلا كاش
        public const string PartnerAccessSettingKey = "ProfessionalModels.PartnerAccess.Enabled";

        private const int BulkThreshold = 20;

        private readonly ApplicationDbContext _context;

        public ProfessionalModelAvailabilityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<ProfessionalModel> AvailableForPartner(int partnerId, ProfessionalModelType? type)
        {
            var today = DateTime.Today;

            return _context.ProfessionalModels.AsNoTracking().Where(m =>
                !m.IsArchived &&
                m.IsForPartners &&
                (type == null || m.ModelType == type) &&
                !_context.SystemSettings.Any(x => x.Key == PartnerAccessSettingKey && x.Value == "false") &&
                _context.PartnerSubscriptions.Any(s =>
                    s.PartnerId == partnerId &&
                    s.StartDate <= today &&
                    s.EndDate >= today &&
                    s.CanUseProfessionalModels) &&
                (m.IsGlobal
                 || _context.ProfessionalModelPartners.Any(p =>
                        p.ProfessionalModelId == m.Id &&
                        p.PartnerId == partnerId)
                 || _context.ProfessionalModelSubscriptionPeriods.Any(sp =>
                        sp.ProfessionalModelId == m.Id &&
                        sp.PartnerSubscriptionPeriod.PartnerSubscription.PartnerId == partnerId &&
                        sp.PartnerSubscriptionPeriod.StartDate <= today &&
                        sp.PartnerSubscriptionPeriod.EndDate >= today)));
        }

        public Task<bool> IsAvailableForPartnerAsync(int modelId, int partnerId, ProfessionalModelType? type, CancellationToken ct = default)
            => AvailableForPartner(partnerId, type).AnyAsync(m => m.Id == modelId, ct);

        public async Task<HashSet<int>> GetPartnerIdsWithAccessAsync(int modelId, ProfessionalModelType? type, CancellationToken ct = default)
        {
            var today = DateTime.Today;

            var model = await _context.ProfessionalModels.AsNoTracking()
                .Where(m =>
                    m.Id == modelId &&
                    !m.IsArchived &&
                    m.IsForPartners &&
                    (type == null || m.ModelType == type) &&
                    !_context.SystemSettings.Any(x => x.Key == PartnerAccessSettingKey && x.Value == "false"))
                .Select(m => new { m.IsGlobal })
                .FirstOrDefaultAsync(ct);

            if (model == null)
                return new HashSet<int>();

            var contracted = await _context.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.StartDate <= today && s.EndDate >= today && s.CanUseProfessionalModels)
                .Select(s => s.PartnerId)
                .Distinct()
                .ToListAsync(ct);

            var result = contracted.ToHashSet();
            if (model.IsGlobal)
                return result;

            var linked = await _context.ProfessionalModelPartners.AsNoTracking()
                .Where(p => p.ProfessionalModelId == modelId)
                .Select(p => p.PartnerId)
                .ToListAsync(ct);

            var viaPeriods = await _context.ProfessionalModelSubscriptionPeriods.AsNoTracking()
                .Where(sp =>
                    sp.ProfessionalModelId == modelId &&
                    sp.PartnerSubscriptionPeriod.StartDate <= today &&
                    sp.PartnerSubscriptionPeriod.EndDate >= today)
                .Select(sp => sp.PartnerSubscriptionPeriod.PartnerSubscription.PartnerId)
                .ToListAsync(ct);

            result.IntersectWith(linked.Concat(viaPeriods));
            return result;
        }

        public async Task<List<PartnerOptionVm>> GetPartnerOptionsAsync(IEnumerable<int> selectedPartnerIds, CancellationToken ct = default)
        {
            var today = DateTime.Today;
            var selected = selectedPartnerIds.ToHashSet();

            var partners = await _context.Partners
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new PartnerOptionVm
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsActive = p.PartnershipEnd >= today
                })
                .ToListAsync(ct);

            return partners.Where(p => p.IsActive || selected.Contains(p.Id)).ToList();
        }

        public Task<List<int>> GetAssignedPartnerIdsAsync(int modelId, CancellationToken ct = default)
            => _context.ProfessionalModelPartners
                .AsNoTracking()
                .Where(p => p.ProfessionalModelId == modelId)
                .Select(p => p.PartnerId)
                .ToListAsync(ct);

        public async Task<List<int>> FilterExistingPartnerIdsAsync(IEnumerable<int> partnerIds, CancellationToken ct = default)
        {
            var requested = partnerIds.ToHashSet();
            if (requested.Count == 0)
                return new List<int>();

            // جدول الشركاء صغير: المقارنة في الذاكرة تتجنب Contains داخل الاستعلام
            var existing = await _context.Partners.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            return existing.Where(requested.Contains).ToList();
        }

        public async Task ApplyPartnerAudienceAsync(ProfessionalModel model, bool isForPartners, PartnerAudience audience, IReadOnlyCollection<int> partnerIds, CancellationToken ct = default)
        {
            model.IsForPartners = isForPartners;

            // «داخلي»: الجمهور يبقى محفوظًا لإعادة التفعيل لكنه لا يُستخدم
            if (!isForPartners)
            {
                await _context.SaveChangesAsync(ct);
                return;
            }

            var isAll = audience == PartnerAudience.All;
            model.IsGlobal = isAll;

            var wanted = isAll ? new HashSet<int>() : partnerIds.ToHashSet();
            var links = await _context.ProfessionalModelPartners
                .Where(p => p.ProfessionalModelId == model.Id)
                .ToListAsync(ct);

            _context.ProfessionalModelPartners.RemoveRange(links.Where(l => !wanted.Contains(l.PartnerId)));

            var linked = links.Select(l => l.PartnerId).ToHashSet();
            var toAdd = wanted
                .Where(id => !linked.Contains(id))
                .Select(id => new ProfessionalModelPartner { ProfessionalModelId = model.Id, PartnerId = id })
                .ToList();

            if (toAdd.Count <= BulkThreshold)
                _context.ProfessionalModelPartners.AddRange(toAdd);

            await _context.SaveChangesAsync(ct);

            if (toAdd.Count > BulkThreshold)
                await _context.BulkInsertAsync(toAdd, cancellationToken: ct);
        }

        public async Task<int> ClassifyUntypedModelsAsync(IEnumerable<int> modelIds, ProfessionalModelType type, CancellationToken ct = default)
        {
            var requested = modelIds.ToHashSet();
            if (requested.Count == 0 || !Enum.IsDefined(type))
                return 0;

            // المقارنة في الذاكرة تتجنب Contains داخل الاستعلام، والنموذج المصنَّف لا يتغير حتى بطلب مصطنع
            var untypedIds = await _context.ProfessionalModels
                .AsNoTracking()
                .Where(m => !m.IsArchived && m.ModelType != ProfessionalModelType.Exam && m.ModelType != ProfessionalModelType.Homework)
                .Select(m => m.Id)
                .ToListAsync(ct);

            var stubs = untypedIds
                .Where(requested.Contains)
                .Select(id => new ProfessionalModel { Id = id, Title = "-", Description = "-", CreatedBy = "-", ModelType = type })
                .ToList();

            if (stubs.Count > BulkThreshold)
            {
                await _context.BulkUpdateAsync(stubs, new BulkConfig
                {
                    PropertiesToInclude = new List<string> { nameof(ProfessionalModel.ModelType) }
                }, cancellationToken: ct);
                return stubs.Count;
            }

            foreach (var stub in stubs)
            {
                _context.ProfessionalModels.Attach(stub);
                _context.Entry(stub).Property(m => m.ModelType).IsModified = true;
            }

            await _context.SaveChangesAsync(ct);
            return stubs.Count;
        }

        public async Task SyncSubscriptionPeriodsAsync(int modelId, IEnumerable<int> periodIds, CancellationToken ct = default)
        {
            var requested = periodIds.ToHashSet();
            var existing = requested.Count == 0
                ? new List<int>()
                : await _context.PartnerSubscriptionPeriods.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            var wanted = existing.Where(requested.Contains).ToHashSet();

            var links = await _context.ProfessionalModelSubscriptionPeriods
                .Where(p => p.ProfessionalModelId == modelId)
                .ToListAsync(ct);

            _context.ProfessionalModelSubscriptionPeriods.RemoveRange(links.Where(l => !wanted.Contains(l.PartnerSubscriptionPeriodId)));

            var linked = links.Select(l => l.PartnerSubscriptionPeriodId).ToHashSet();
            var toAdd = wanted
                .Where(id => !linked.Contains(id))
                .Select(id => new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = modelId, PartnerSubscriptionPeriodId = id })
                .ToList();

            if (toAdd.Count <= BulkThreshold)
                _context.ProfessionalModelSubscriptionPeriods.AddRange(toAdd);

            await _context.SaveChangesAsync(ct);

            if (toAdd.Count > BulkThreshold)
                await _context.BulkInsertAsync(toAdd, cancellationToken: ct);
        }
    }
}
