using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.ProfessionalModels;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S1.2: المصدر الوحيد لدفعات الجمهور وأهلية الشركاء والتحقق من طلب الإرسال.
    // كل قوائم الـ Id من الطلب تُمرَّر بـ EF.Constant (قائمة ثابتة داخل IN) لتجنب OPENJSON على SQL Server 2014.
    public class DispatchAudienceService : IDispatchAudienceService
    {
        private const string ActiveEnrollmentStatus = "Active";

        private readonly ApplicationDbContext _context;
        private readonly IProfessionalModelAvailabilityService _availability;

        public DispatchAudienceService(ApplicationDbContext context, IProfessionalModelAvailabilityService availability)
        {
            _context = context;
            _availability = availability;
        }

        private sealed record ResolvedSource(DispatchSource Source, int? CurriculumId, int? CourseId);

        private sealed record PartnerContract(bool Homework, bool Exams, bool Placement, bool PerformanceIndicator, bool MinistrySim);

        private sealed record Eligibility(bool Eligible, string? BlockReason, bool Overridable);

        private sealed class PartnerBatchRow
        {
            public int PartnerId { get; init; }
            public int BatchId { get; init; }
            public string Name { get; init; } = "";
            public string BranchName { get; init; } = "";
            public int ActiveStudents { get; init; }
        }

        // =====================================================
        // القوائم
        // =====================================================
        public async Task<IReadOnlyList<DispatchBatchVm>> GetPlatformBatchesAsync(DispatchSource source, CancellationToken ct = default)
        {
            var resolved = await ResolveAsync(source, ct);

            return await PlatformBatches(resolved)
                .OrderBy(b => b.Name)
                .Select(b => new DispatchBatchVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    BranchName = b.Branch.Name,
                    ActiveStudents = _context.StudentBatchEnrollments.Count(e =>
                        e.BatchId == b.Id &&
                        e.Status == ActiveEnrollmentStatus)
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PartnerEligibilityVm>> GetPartnersAsync(DispatchSource source, CancellationToken ct = default)
        {
            var resolved = await ResolveAsync(source, ct);
            var today = DateTime.Today;

            var contracts = await LoadContractsAsync(null, ct);

            var partners = await _context.Partners.AsNoTracking()
                .Select(p => new { p.Id, p.Name, p.PartnershipEnd })
                .ToListAsync(ct);

            var listed = partners
                .Where(p => p.PartnershipEnd >= today || contracts.ContainsKey(p.Id))
                .OrderBy(p => p.Name)
                .ToList();

            if (listed.Count == 0)
                return Array.Empty<PartnerEligibilityVm>();

            var modelAccess = await LoadModelAccessAsync(resolved, ct);

            var batches = await PartnerBatches(resolved)
                .OrderBy(b => b.Name)
                .Select(b => new PartnerBatchRow
                {
                    PartnerId = b.Branch.PartnerId!.Value,
                    BatchId = b.Id,
                    Name = b.Name,
                    BranchName = b.Branch.Name,
                    ActiveStudents = _context.StudentBatchEnrollments.Count(e =>
                        e.BatchId == b.Id &&
                        e.Status == ActiveEnrollmentStatus &&
                        e.Student.IsActiveForLearning &&
                        e.Student.Branch.PartnerId == b.Branch.PartnerId)
                })
                .ToListAsync(ct);

            var batchesByPartner = batches.ToLookup(b => b.PartnerId);

            return listed
                .Select(p =>
                {
                    var eligibility = Evaluate(resolved.Source, contracts.GetValueOrDefault(p.Id), modelAccess, p.Id);
                    return new PartnerEligibilityVm
                    {
                        PartnerId = p.Id,
                        Name = p.Name,
                        Eligible = eligibility.Eligible,
                        BlockReason = eligibility.BlockReason,
                        Overridable = eligibility.Overridable,
                        Batches = batchesByPartner[p.Id]
                            .Select(b => new DispatchBatchVm
                            {
                                BatchId = b.BatchId,
                                Name = b.Name,
                                BranchName = b.BranchName,
                                ActiveStudents = b.ActiveStudents
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        // =====================================================
        // التحقق
        // =====================================================
        public async Task<AudienceValidation> ValidateAsync(DispatchSource source, AudienceSelection selection, bool canOverride, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(selection);

            var resolved = await ResolveAsync(source, ct);
            var errors = new List<string>();

            var platformIds = (selection.PlatformBatchIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var partnerSelections = (selection.PartnerBatchIds ?? new Dictionary<int, List<int>>())
                .Select(kv => new
                {
                    PartnerId = kv.Key,
                    BatchIds = (kv.Value ?? new List<int>()).Where(id => id > 0).Distinct().ToList()
                })
                .Where(x => x.BatchIds.Count > 0)
                .ToList();

            if (selection.Audience == DispatchAudience.Platform)
            {
                if (partnerSelections.Count > 0)
                    errors.Add("لا يمكن اختيار دفعات المدارس الشريكة تحت «دفعات المنصة».");

                if (platformIds.Count > 0)
                {
                    var valid = await PlatformBatches(resolved)
                        .Where(b => EF.Constant(platformIds).Contains(b.Id))
                        .Select(b => b.Id)
                        .ToListAsync(ct);

                    if (valid.Count != platformIds.Count)
                        errors.Add("دفعة أو أكثر ليست من دفعات المنصة أو لم تعد متاحة.");
                }

                return Result(DispatchAudience.Platform, errors, platformIds, new List<PartnerDispatchTarget>());
            }

            if (selection.Audience != DispatchAudience.Partners)
            {
                errors.Add("جمهور الإرسال غير صالح.");
                return Result(selection.Audience, errors, new List<int>(), new List<PartnerDispatchTarget>());
            }

            if (platformIds.Count > 0)
                errors.Add("لا يمكن اختيار دفعات المنصة تحت «المدارس الشريكة».");

            if (partnerSelections.Count == 0)
            {
                errors.Add("اختر مدرسة شريكة ودفعة واحدة على الأقل.");
                return Result(DispatchAudience.Partners, errors, new List<int>(), new List<PartnerDispatchTarget>());
            }

            var partnerIds = partnerSelections.Select(x => x.PartnerId).ToList();
            var allBatchIds = partnerSelections.SelectMany(x => x.BatchIds).Distinct().ToList();

            var names = await _context.Partners.AsNoTracking()
                .Where(p => EF.Constant(partnerIds).Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            var batchOwners = await PartnerBatches(resolved)
                .Where(b => EF.Constant(allBatchIds).Contains(b.Id))
                .Select(b => new { b.Id, PartnerId = b.Branch.PartnerId!.Value })
                .ToDictionaryAsync(b => b.Id, b => b.PartnerId, ct);

            var contracts = await LoadContractsAsync(partnerIds, ct);
            var modelAccess = await LoadModelAccessAsync(resolved, ct);

            var reason = selection.OverrideReason?.Trim();
            var overrideChecked = false;
            var overrideAllowed = false;
            var targets = new List<PartnerDispatchTarget>();

            foreach (var item in partnerSelections)
            {
                if (!names.TryGetValue(item.PartnerId, out var name))
                {
                    errors.Add("مدرسة شريكة غير موجودة.");
                    continue;
                }

                if (item.BatchIds.Any(id => !batchOwners.TryGetValue(id, out var owner) || owner != item.PartnerId))
                    errors.Add($"{name}: دفعة أو أكثر لا تخص هذه المدرسة أو لم تعد متاحة لهذا الإرسال.");

                var eligibility = Evaluate(resolved.Source, contracts.GetValueOrDefault(item.PartnerId), modelAccess, item.PartnerId);
                var overridden = false;

                if (!eligibility.Eligible)
                {
                    if (!eligibility.Overridable || !selection.OverrideContract)
                    {
                        errors.Add($"{name}: {eligibility.BlockReason}.");
                        continue;
                    }

                    if (!overrideChecked)
                    {
                        overrideChecked = true;
                        overrideAllowed = CheckOverride(canOverride, reason, errors);
                    }

                    if (!overrideAllowed)
                        continue;

                    overridden = true;
                }

                targets.Add(new PartnerDispatchTarget
                {
                    PartnerId = item.PartnerId,
                    PartnerName = name,
                    BatchIds = item.BatchIds,
                    ContractOverridden = overridden,
                    OverrideReason = overridden ? reason : null
                });
            }

            return Result(DispatchAudience.Partners, errors, new List<int>(), targets);
        }

        private static bool CheckOverride(bool canOverride, string? reason, List<string> errors)
        {
            if (!canOverride)
            {
                errors.Add("لا تملك صلاحية تجاوز عقد المدرسة الشريكة.");
                return false;
            }

            var length = reason?.Length ?? 0;
            if (length < DispatchRules.OverrideReasonMinLength || length > DispatchRules.OverrideReasonMaxLength)
            {
                errors.Add($"سبب تجاوز العقد مطلوب بين {DispatchRules.OverrideReasonMinLength} و{DispatchRules.OverrideReasonMaxLength} حرف.");
                return false;
            }

            return true;
        }

        // عند وجود أخطاء لا تُرجع أي هدف حتى لا يُرسل جزء من طلب غير صالح
        private static AudienceValidation Result(DispatchAudience audience, List<string> errors, List<int> platformIds, List<PartnerDispatchTarget> targets)
        {
            var result = new AudienceValidation
            {
                Audience = audience,
                PlatformBatchIds = errors.Count == 0 ? platformIds : new List<int>(),
                Partners = errors.Count == 0 ? targets : new List<PartnerDispatchTarget>()
            };
            result.Errors.AddRange(errors.Distinct());
            return result;
        }

        // =====================================================
        // الأهلية
        // =====================================================
        private static Eligibility Evaluate(DispatchSource source, PartnerContract? contract, HashSet<int> modelAccess, int partnerId)
        {
            if (contract == null)
                return new Eligibility(false, DispatchRules.NoActiveContract, false);

            if (!HasFeature(contract, source.Kind))
                return new Eligibility(false, DispatchRules.FeatureMissing(source.Kind), true);

            if (source.IsFromProfessionalModel && !modelAccess.Contains(partnerId))
                return new Eligibility(false, DispatchRules.ModelNotAvailable, true);

            return new Eligibility(true, null, true);
        }

        private static bool HasFeature(PartnerContract contract, PartnerDispatchKind kind) => kind switch
        {
            PartnerDispatchKind.Homework => contract.Homework,
            PartnerDispatchKind.Exam => contract.Exams,
            PartnerDispatchKind.Placement => contract.Placement,
            PartnerDispatchKind.PerformanceIndicator => contract.PerformanceIndicator,
            PartnerDispatchKind.MinistrySim => contract.MinistrySim,
            _ => false
        };

        // عقد نشط بالتاريخ. أكثر من عقد نشط لنفس الشريك: الميزة متاحة إن شملها أي منها.
        private async Task<Dictionary<int, PartnerContract>> LoadContractsAsync(List<int>? partnerIds, CancellationToken ct)
        {
            var today = DateTime.Today;

            var query = _context.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.StartDate <= today && s.EndDate >= today);

            if (partnerIds != null)
                query = query.Where(s => EF.Constant(partnerIds).Contains(s.PartnerId));

            var rows = await query
                .Select(s => new
                {
                    s.PartnerId,
                    s.CanCreateHomework,
                    s.CanCreateExams,
                    s.CanUsePlacementExams,
                    s.CanUsePerformanceIndicatorExams,
                    s.CanUseMinistrySimExams
                })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.PartnerId)
                .ToDictionary(
                    g => g.Key,
                    g => new PartnerContract(
                        g.Any(r => r.CanCreateHomework),
                        g.Any(r => r.CanCreateExams),
                        g.Any(r => r.CanUsePlacementExams),
                        g.Any(r => r.CanUsePerformanceIndicatorExams),
                        g.Any(r => r.CanUseMinistrySimExams)));
        }

        // نوع النموذج لا يُشترط هنا: توافق النوع مع الشاشة مسؤولية خدمة الإرسال
        private async Task<HashSet<int>> LoadModelAccessAsync(ResolvedSource resolved, CancellationToken ct)
        {
            if (!resolved.Source.IsFromProfessionalModel || !resolved.Source.ProfessionalModelId.HasValue)
                return new HashSet<int>();

            return await _availability.GetPartnerIdsWithAccessAsync(resolved.Source.ProfessionalModelId.Value, null, ct);
        }

        // =====================================================
        // الدفعات
        // =====================================================
        private async Task<ResolvedSource> ResolveAsync(DispatchSource source, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!source.IsFromProfessionalModel)
                return new ResolvedSource(source, source.CurriculumId, source.CourseId);

            // منهج النموذج من القاعدة لا من الطلب. نموذج غير موجود أو بلا Id: لا منهج ولا إتاحة لأي شريك.
            int? curriculumId = null;
            if (source.ProfessionalModelId.HasValue)
            {
                curriculumId = await _context.ProfessionalModels.AsNoTracking()
                    .Where(m => m.Id == source.ProfessionalModelId.Value)
                    .Select(m => m.CurriculumId)
                    .FirstOrDefaultAsync(ct);
            }

            return new ResolvedSource(source, curriculumId, source.CourseId);
        }

        private IQueryable<Batch> ActiveBatches(int? courseId)
        {
            var batches = _context.Batches.AsNoTracking().Where(b => !b.IsDeleted && !b.IsArchived);

            if (courseId.HasValue)
                batches = batches.Where(b => b.CourseId == courseId.Value);

            return batches;
        }

        private IQueryable<Batch> PlatformBatches(ResolvedSource resolved)
            => ActiveBatches(resolved.CourseId).Where(b => b.Branch.PartnerId == null);

        // دفعات الشركاء: متوافقة مع منهج المصدر عند وجوده
        private IQueryable<Batch> PartnerBatches(ResolvedSource resolved)
        {
            var batches = ActiveBatches(resolved.CourseId).Where(b => b.Branch.PartnerId != null);

            if (resolved.CurriculumId.HasValue)
            {
                var curriculumId = resolved.CurriculumId.Value;
                batches = batches.Where(b => _context.CourseCurriculums.Any(cc => cc.CourseId == b.CourseId && cc.CurriculumId == curriculumId));
            }

            return batches;
        }
    }
}
