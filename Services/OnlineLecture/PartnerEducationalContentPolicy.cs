using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S4.1 — حالة بند «المحتوى التعليمي» (<see cref="PartnerSubscription.CanAccessEducationalContent"/>) في عقد الشريك
    /// كما تُعرض للأدمن (R16/R18). الأرقام ثابتة لأن الشاشة ترسلها كنص فقط.
    /// </summary>
    public enum PartnerEducationalContentState
    {
        /// <summary>عقد نشط الآن ويشمل البند.</summary>
        Enabled = 1,
        /// <summary>عقد نشط الآن لا يشمل البند.</summary>
        NotInContract = 2,
        /// <summary>لا يوجد عقد نشط الآن.</summary>
        NoActiveContract = 3
    }

    /// <summary>نتيجة فحص دفعة قبل تفعيل التعليم أونلاين (R16). <see cref="PartnerId"/> = null ⇒ دفعة منصة.</summary>
    public sealed record PartnerBatchActivationCheck(int? PartnerId, string? PartnerName, PartnerEducationalContentState State)
    {
        public bool IsPartnerBatch => PartnerId.HasValue;
        public bool CanActivate => !IsPartnerBatch || State == PartnerEducationalContentState.Enabled;
    }

    /// <summary>
    /// OLC-S4.1 — قواعد بند العقد كتعبيرات EF (بلا OPENJSON) حتى تُدمج داخل أي Join (مثل وصول الطالب في OLC-S5)
    /// ولا يُستعلم لكل دفعة/محاضرة. «اليوم» = DateTime.Today كبقية فحوص العقد في PartnerSubscriptionService.
    /// </summary>
    public static class PartnerEducationalContentRules
    {
        /// <summary>R16: عقد نشط الآن (StartDate ≤ اليوم ≤ EndDate).</summary>
        public static Expression<Func<PartnerSubscription, bool>> ActiveNow(DateTime today)
            => s => s.StartDate <= today && s.EndDate >= today;

        /// <summary>
        /// R17: العقد ما زال ساريًا للطلاب حتى AccessUntilDate ?? EndDate. يُطبَّق كـ «EndDate أو AccessUntilDate لم يمضِ»
        /// — مطابق للعقد متى كان AccessUntilDate ≥ EndDate (الحالة الطبيعية)، ولا يقطع وصول طلاب عقد ما زال نشطًا
        /// إن أُدخل AccessUntilDate أقدم من EndDate خطأً.
        /// </summary>
        public static Expression<Func<PartnerSubscription, bool>> StudentWindow(DateTime today)
            => s => s.StartDate <= today
                 && (s.EndDate >= today || (s.AccessUntilDate != null && s.AccessUntilDate >= today));

        /// <summary>R17 مدمجًا: البند مفعّل + النافذة.</summary>
        public static Expression<Func<PartnerSubscription, bool>> StudentAccess(DateTime today)
            => s => s.CanAccessEducationalContent
                 && s.StartDate <= today
                 && (s.EndDate >= today || (s.AccessUntilDate != null && s.AccessUntilDate >= today));

        /// <summary>
        /// R2/R3 + R17 على مستوى الدفعة: دفعة المنصة (Branch.PartnerId == null) متاحة دائمًا، ودفعة الشريك متاحة لطلابها
        /// فقط إن كان لشريكها عقد ضمن نافذة الطلاب يشمل البند. يُستخدم في OLC-S5 داخل نفس الاستعلام.
        /// </summary>
        public static IQueryable<Batch> WhereStudentAccessible(this IQueryable<Batch> batches, IQueryable<PartnerSubscription> subscriptions, DateTime today)
            => batches.Where(b => b.Branch.PartnerId == null
                || subscriptions.Any(s => s.PartnerId == b.Branch.PartnerId
                    && s.CanAccessEducationalContent
                    && s.StartDate <= today
                    && (s.EndDate >= today || (s.AccessUntilDate != null && s.AccessUntilDate >= today))));
    }

    /// <summary>
    /// OLC-S4.1 — المصدر الوحيد لقرارات بند «المحتوى التعليمي» في عقد الشريك (R16/R17/R19). لا عمود جديد:
    /// البند الموجود CanAccessEducationalContent هو «المحتوى التعليمي أونلاين لدفعات الشريك».
    /// أكثر من عقد لنفس الشريك: يكفي أن يشمل البند أي عقد مستوفٍ (نفس قرار APD).
    /// </summary>
    public interface IPartnerEducationalContentPolicy
    {
        /// <summary>R16: حالة البند الآن لشريك واحد.</summary>
        Task<PartnerEducationalContentState> GetStateAsync(int partnerId, CancellationToken ct = default);

        /// <summary>R18: حالة البند لكل الشركاء الذين لهم أي عقد (استعلام واحد). الغائب = لا عقد نشط.</summary>
        Task<IReadOnlyDictionary<int, PartnerEducationalContentState>> GetStatesAsync(CancellationToken ct = default);

        /// <summary>R17: هل يصل طلاب هذا الشريك للمحتوى الآن؟ (البند + حتى AccessUntilDate ?? EndDate).</summary>
        Task<bool> CanStudentsAccessAsync(int partnerId, CancellationToken ct = default);

        /// <summary>
        /// R19: قراءة الشريك لصفحة «المحتوى التعليمي» — عقد نشط يشمل البند، أو (لا عقد نشط) وآخر عقد بدأ يشمله وما زال
        /// ضمن AccessUntilDate. نفس قرار [PartnerFeature(EducationalContent, AllowReadAfterExpiry = true)].
        /// </summary>
        Task<bool> CanPartnerReadAsync(int partnerId, CancellationToken ct = default);

        /// <summary>R16 لدفعة: null = الدفعة غير موجودة/محذوفة. دفعة المنصة تُرجع PartnerId = null.</summary>
        Task<PartnerBatchActivationCheck?> CheckBatchAsync(int batchId, CancellationToken ct = default);

        /// <summary>رسالة عربية موحّدة لسبب رفض التفعيل.</summary>
        string DescribeBlock(PartnerBatchActivationCheck check);
    }

    public sealed class PartnerEducationalContentPolicy : IPartnerEducationalContentPolicy
    {
        private readonly ApplicationDbContext _db;

        public PartnerEducationalContentPolicy(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PartnerEducationalContentState> GetStateAsync(int partnerId, CancellationToken ct = default)
        {
            var today = DateTime.Today;
            var clauses = await _db.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.PartnerId == partnerId)
                .Where(PartnerEducationalContentRules.ActiveNow(today))
                .Select(s => s.CanAccessEducationalContent)
                .ToListAsync(ct);

            return ToState(clauses);
        }

        public async Task<IReadOnlyDictionary<int, PartnerEducationalContentState>> GetStatesAsync(CancellationToken ct = default)
        {
            var today = DateTime.Today;
            var rows = await _db.PartnerSubscriptions.AsNoTracking()
                .Where(PartnerEducationalContentRules.ActiveNow(today))
                .Select(s => new { s.PartnerId, s.CanAccessEducationalContent })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.PartnerId)
                .ToDictionary(g => g.Key, g => ToState(g.Select(r => r.CanAccessEducationalContent).ToList()));
        }

        public Task<bool> CanStudentsAccessAsync(int partnerId, CancellationToken ct = default)
            => _db.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.PartnerId == partnerId)
                .AnyAsync(PartnerEducationalContentRules.StudentAccess(DateTime.Today), ct);

        public async Task<bool> CanPartnerReadAsync(int partnerId, CancellationToken ct = default)
        {
            var today = DateTime.Today;

            var activeClauses = await _db.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.PartnerId == partnerId)
                .Where(PartnerEducationalContentRules.ActiveNow(today))
                .Select(s => s.CanAccessEducationalContent)
                .ToListAsync(ct);

            if (activeClauses.Count > 0)
                return activeClauses.Any(c => c);

            // بلا عقد نشط: آخر عقد بدأ (بالانتهاء) يحكم القراءة حتى AccessUntilDate — مطابق لـ CanReadFeatureAfterExpiry
            var last = await _db.PartnerSubscriptions.AsNoTracking()
                .Where(s => s.PartnerId == partnerId && s.StartDate <= today)
                .OrderByDescending(s => s.EndDate)
                .Select(s => new { s.CanAccessEducationalContent, s.AccessUntilDate })
                .FirstOrDefaultAsync(ct);

            return last is { CanAccessEducationalContent: true, AccessUntilDate: not null }
                && today <= last.AccessUntilDate.Value;
        }

        public async Task<PartnerBatchActivationCheck?> CheckBatchAsync(int batchId, CancellationToken ct = default)
        {
            // الفرع باستعلام مستقل (لا Branch navigation) حتى لا تختفي الدفعة إن تعذّر Join الفرع
            var batch = await _db.Batches.AsNoTracking()
                .Where(b => b.Id == batchId && !b.IsDeleted)
                .Select(b => new { b.BranchId })
                .FirstOrDefaultAsync(ct);

            if (batch is null)
                return null;

            var partnerId = await _db.Branches.AsNoTracking()
                .Where(br => br.Id == batch.BranchId)
                .Select(br => br.PartnerId)
                .FirstOrDefaultAsync(ct);

            if (!partnerId.HasValue)
                return new PartnerBatchActivationCheck(null, null, PartnerEducationalContentState.Enabled);

            var partnerName = await _db.Partners.AsNoTracking()
                .Where(p => p.Id == partnerId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct);

            var state = await GetStateAsync(partnerId.Value, ct);
            return new PartnerBatchActivationCheck(partnerId, partnerName, state);
        }

        public string DescribeBlock(PartnerBatchActivationCheck check)
        {
            var name = string.IsNullOrWhiteSpace(check.PartnerName) ? "الشريك" : $"الشريك «{check.PartnerName}»";
            return check.State switch
            {
                PartnerEducationalContentState.NoActiveContract =>
                    $"لا يمكن تفعيل التعليم أونلاين لهذه الدفعة: لا يوجد لـ{name} عقد نشط اليوم. جدّد العقد أولًا.",
                PartnerEducationalContentState.NotInContract =>
                    $"لا يمكن تفعيل التعليم أونلاين لهذه الدفعة: عقد {name} لا يشمل بند «المحتوى التعليمي». فعّل البند في العقد أولًا.",
                _ => string.Empty
            };
        }

        private static PartnerEducationalContentState ToState(IReadOnlyCollection<bool> activeClauses)
        {
            if (activeClauses.Count == 0)
                return PartnerEducationalContentState.NoActiveContract;

            return activeClauses.Any(c => c)
                ? PartnerEducationalContentState.Enabled
                : PartnerEducationalContentState.NotInContract;
        }
    }
}
