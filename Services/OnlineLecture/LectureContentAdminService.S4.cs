using Microsoft.EntityFrameworkCore;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S4.2 — دفعات الشركاء: معرّف الشريك لأي دفعة/محاضرة (لفحص صلاحية LectureContents:ManagePartners — R18)
    /// وشاشة «الدفعات الأونلاين» بجمهور «المنصة/الشركاء» وحالة البند لكل شريك.
    /// </summary>
    public sealed partial class LectureContentAdminService
    {
        private const string ActiveEnrollmentStatus = "Active";

        public Task<int?> GetBatchPartnerIdAsync(int batchId, CancellationToken ct = default)
            => (from b in _db.Batches.AsNoTracking()
                join br in _db.Branches.AsNoTracking() on b.BranchId equals br.Id
                where b.Id == batchId
                select br.PartnerId)
               .FirstOrDefaultAsync(ct);

        public Task<int?> GetLecturePartnerIdAsync(int lectureId, CancellationToken ct = default)
            => (from l in _db.Lecture.AsNoTracking()
                join b in _db.Batches.AsNoTracking() on l.BatchId equals b.Id
                join br in _db.Branches.AsNoTracking() on b.BranchId equals br.Id
                where l.Id == lectureId
                select br.PartnerId)
               .FirstOrDefaultAsync(ct);

        public async Task<OnlineBatchesPageVm> GetOnlineBatchesAsync(OnlineBatchesFilterVm filter, bool canManagePartners, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            // بلا ManagePartners: «المنصة» فقط (المستدعي يرفض طلب «الشركاء» المصطنع قبل الوصول هنا)
            var audience = canManagePartners && filter.Audience == DispatchAudience.Partners
                ? DispatchAudience.Partners
                : DispatchAudience.Platform;

            var page = new OnlineBatchesPageVm
            {
                Audience = audience,
                IncludeInactive = filter.IncludeInactive,
                CanManagePartners = canManagePartners
            };

            var batches = _db.Batches.AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Where(b => b.IsOnlineLearning || (filter.IncludeInactive && !b.IsArchived));

            IReadOnlyDictionary<int, PartnerEducationalContentState> states = new Dictionary<int, PartnerEducationalContentState>();

            if (audience == DispatchAudience.Platform)
            {
                batches = batches.Where(b => b.Branch.PartnerId == null);
            }
            else
            {
                states = await _partnerPolicy.GetStatesAsync(ct);
                var today = DateTime.Today;

                var partners = await _db.Partners.AsNoTracking()
                    .OrderBy(p => p.Name)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.PartnershipEnd,
                        OnlineBatches = _db.Batches.Count(b => !b.IsDeleted && b.IsOnlineLearning && b.Branch.PartnerId == p.Id)
                    })
                    .ToListAsync(ct);

                // الشركاء المطلوبون يُطابقون على القائمة المحمّلة من الخادم (لا يُوثق بالمعرّفات القادمة)
                var requested = (filter.PartnerIds ?? new List<int>()).Where(id => id > 0).ToHashSet();
                var listed = partners
                    .Where(p => p.PartnershipEnd >= today || states.ContainsKey(p.Id) || p.OnlineBatches > 0)
                    .ToList();
                var selected = listed.Where(p => requested.Contains(p.Id)).Select(p => p.Id).ToList();

                page.SelectedPartnerIds = selected;
                page.Partners = listed
                    .Select(p => new OnlineBatchesPartnerVm
                    {
                        PartnerId = p.Id,
                        Name = p.Name ?? string.Empty,
                        State = states.TryGetValue(p.Id, out var s) ? s : PartnerEducationalContentState.NoActiveContract,
                        OnlineBatchesCount = p.OnlineBatches,
                        IsSelected = selected.Contains(p.Id)
                    })
                    .ToList();

                // لا اختيار = كل الشركاء. قائمة ثابتة داخل IN (EF.Constant) لتجنب OPENJSON على SQL Server 2014.
                batches = selected.Count > 0
                    ? batches.Where(b => b.Branch.PartnerId != null && EF.Constant(selected).Contains(b.Branch.PartnerId!.Value))
                    : batches.Where(b => b.Branch.PartnerId != null);
            }

            var rows = await batches
                .Select(b => new OnlineBatchRowVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    BranchName = b.Branch.Name,
                    CourseName = b.Course != null ? b.Course.Name : null,
                    PartnerId = b.Branch.PartnerId,
                    PartnerName = b.Branch.Partner != null ? b.Branch.Partner.Name : null,
                    IsOnlineLearning = b.IsOnlineLearning,
                    OnlineLearningEnabledAt = b.OnlineLearningEnabledAt,
                    IsArchived = b.IsArchived,
                    ActiveStudents = _db.StudentBatchEnrollments.Count(e => e.BatchId == b.Id && e.Status == ActiveEnrollmentStatus),
                    LecturesCount = _db.Lecture.Count(l => l.BatchId == b.Id),
                    ContentsCount = _db.LectureContents.Count(c => c.Lecture.BatchId == b.Id),
                    PublishedCount = _db.LectureContents.Count(c => c.Lecture.BatchId == b.Id && c.Status == LectureContentStatus.Published)
                })
                .ToListAsync(ct);

            foreach (var row in rows.Where(r => r.PartnerId.HasValue))
                row.ContractState = states.TryGetValue(row.PartnerId!.Value, out var s) ? s : PartnerEducationalContentState.NoActiveContract;

            page.Batches = rows
                .OrderBy(r => r.PartnerName ?? string.Empty)
                .ThenByDescending(r => r.IsOnlineLearning)
                .ThenBy(r => r.Name)
                .ToList();

            return page;
        }
    }
}
