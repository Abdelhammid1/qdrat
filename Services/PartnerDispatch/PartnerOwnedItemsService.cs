using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S2.3: تعديل عنوان ومواعيد ما يملكه الشريك (OwnerPartnerId) من منطقته، وتمييز ما أرسله المعهد باسمه.
    public interface IPartnerOwnedItemsService
    {
        Task<PartnerOwnedItemEditVm?> GetHomeworkAsync(int partnerId, int homeworkSetId, CancellationToken ct = default);

        // null = لا يملكه هذا الشريك. غير ذلك: رسالة الخطأ، أو نص فارغ عند النجاح.
        Task<string?> UpdateHomeworkAsync(int partnerId, PartnerOwnedItemEditVm vm, CancellationToken ct = default);

        Task<PartnerOwnedItemEditVm?> GetExamAssignmentAsync(int partnerId, int examAssignmentId, CancellationToken ct = default);

        Task<string?> UpdateExamAssignmentAsync(int partnerId, PartnerOwnedItemEditVm vm, CancellationToken ct = default);
    }

    public sealed class PartnerOwnedItemEditVm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب.")]
        [StringLength(PartnerOwnedItemsService.MaxTitleLength, ErrorMessage = "العنوان طويل جدًا.")]
        public string Title { get; set; } = "";

        // توقيت السعودية كما يظهر في النموذج
        [Required(ErrorMessage = "موعد البداية مطلوب.")]
        public DateTime? StartAtLocal { get; set; }

        [Required(ErrorMessage = "موعد النهاية مطلوب.")]
        public DateTime? EndAtLocal { get; set; }

        public string BatchName { get; set; } = "";
    }

    public class PartnerOwnedItemsService : IPartnerOwnedItemsService
    {
        public const int MaxTitleLength = 200;

        private readonly ApplicationDbContext _context;
        private readonly ITimeZoneService _timeZone;

        public PartnerOwnedItemsService(ApplicationDbContext context, ITimeZoneService timeZone)
        {
            _context = context;
            _timeZone = timeZone;
        }

        // =====================================================
        // الواجب: ما أُرسل من نموذج يُخزن UTC، ومسودة الشريك تخزن وقت السعودية كما أُدخل
        // =====================================================
        public async Task<PartnerOwnedItemEditVm?> GetHomeworkAsync(int partnerId, int homeworkSetId, CancellationToken ct = default)
        {
            var row = await _context.HomeworkSets
                .AsNoTracking()
                .Where(h => h.Id == homeworkSetId && h.OwnerPartnerId == partnerId && !h.IsArchived)
                .Select(h => new { h.Id, h.Title, h.StartAt, h.EndAt, h.SourceProfessionalModelId, BatchName = h.Batch.Name })
                .FirstOrDefaultAsync(ct);

            if (row == null)
                return null;

            var storedAsUtc = row.SourceProfessionalModelId != null;

            return new PartnerOwnedItemEditVm
            {
                Id = row.Id,
                Title = row.Title,
                StartAtLocal = ToLocal(row.StartAt, storedAsUtc),
                EndAtLocal = ToLocal(row.EndAt, storedAsUtc),
                BatchName = row.BatchName
            };
        }

        public async Task<string?> UpdateHomeworkAsync(int partnerId, PartnerOwnedItemEditVm vm, CancellationToken ct = default)
        {
            var set = await _context.HomeworkSets
                .FirstOrDefaultAsync(h => h.Id == vm.Id && h.OwnerPartnerId == partnerId && !h.IsArchived, ct);

            if (set == null)
                return null;

            var error = Validate(vm);
            if (error != null)
                return error;

            var storedAsUtc = set.SourceProfessionalModelId != null;

            set.Title = vm.Title.Trim();
            set.StartAt = ToStored(vm.StartAtLocal, storedAsUtc);
            set.EndAt = ToStored(vm.EndAtLocal, storedAsUtc);

            await _context.SaveChangesAsync(ct);
            return "";
        }

        // =====================================================
        // الاختبار المملوك للشريك يُخزن UTC. تحديد المستوى له صفحاته وقواعده الخاصة.
        // =====================================================
        public async Task<PartnerOwnedItemEditVm?> GetExamAssignmentAsync(int partnerId, int examAssignmentId, CancellationToken ct = default)
        {
            var row = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(a => a.Id == examAssignmentId &&
                            a.Exam.OwnerPartnerId == partnerId &&
                            a.Exam.Type != ExamType.LevelAssessment &&
                            !a.IsArchived)
                .Select(a => new { a.Id, a.Title, a.ScheduledDate, a.EndAt, BatchName = a.Batch.Name })
                .FirstOrDefaultAsync(ct);

            if (row == null)
                return null;

            return new PartnerOwnedItemEditVm
            {
                Id = row.Id,
                Title = row.Title ?? "",
                StartAtLocal = ToLocal(row.ScheduledDate, true),
                EndAtLocal = ToLocal(row.EndAt, true),
                BatchName = row.BatchName
            };
        }

        public async Task<string?> UpdateExamAssignmentAsync(int partnerId, PartnerOwnedItemEditVm vm, CancellationToken ct = default)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a =>
                    a.Id == vm.Id &&
                    a.Exam.OwnerPartnerId == partnerId &&
                    a.Exam.Type != ExamType.LevelAssessment &&
                    !a.IsArchived,
                    ct);

            if (assignment == null)
                return null;

            var error = Validate(vm);
            if (error != null)
                return error;

            assignment.Title = vm.Title.Trim();
            assignment.Exam.Title = assignment.Title;
            assignment.ScheduledDate = ToStored(vm.StartAtLocal, true);
            assignment.EndAt = ToStored(vm.EndAtLocal, true);

            await _context.SaveChangesAsync(ct);
            return "";
        }

        private static string? Validate(PartnerOwnedItemEditVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Title))
                return "⚠️ العنوان مطلوب.";

            if (vm.Title.Trim().Length > MaxTitleLength)
                return $"⚠️ العنوان يجب ألا يتجاوز {MaxTitleLength} حرفًا.";

            if (!vm.StartAtLocal.HasValue || !vm.EndAtLocal.HasValue)
                return "⚠️ حدد موعد البداية والنهاية.";

            return vm.EndAtLocal <= vm.StartAtLocal
                ? "⚠️ موعد النهاية يجب أن يكون بعد موعد البداية."
                : null;
        }

        private DateTime? ToLocal(DateTime? value, bool storedAsUtc)
            => value.HasValue && storedAsUtc ? _timeZone.ConvertToSaudi(value.Value) : value;

        private DateTime? ToStored(DateTime? local, bool storeAsUtc)
            => local.HasValue && storeAsUtc ? _timeZone.ConvertToUtc(local.Value) : local;
    }
}
