using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ مدخلات (Form) — لا Entities ولا RowVersion من العميل (منع Overposting) ============

    public sealed class CreateRemedialTrackInput
    {
        [Required(ErrorMessage = "اسم الخطة مطلوب")]
        [StringLength(200, ErrorMessage = "اسم الخطة أطول من 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "الوصف أطول من 1000 حرف")]
        public string? Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر المنهج")]
        public int CurriculumId { get; set; }

        [Range(1, 100, ErrorMessage = "نسبة النجاح بين 1 و100")]
        public int PassPercent { get; set; } = 60;

        [Range(50, 100, ErrorMessage = "حد المشاهدة بين 50 و100")]
        public int MinWatchPercent { get; set; } = 90;
    }

    public sealed class EditRemedialTrackHeaderInput
    {
        [Range(1, int.MaxValue)] public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخطة مطلوب")]
        [StringLength(200, ErrorMessage = "اسم الخطة أطول من 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "الوصف أطول من 1000 حرف")]
        public string? Description { get; set; }

        // تُقبل فقط قبل قفل بنية الخطة (D12). null = بلا تغيير.
        [Range(1, int.MaxValue, ErrorMessage = "المنهج غير صالح")]
        public int? CurriculumId { get; set; }

        [Range(1, 100, ErrorMessage = "نسبة النجاح بين 1 و100")]
        public int? PassPercent { get; set; }

        [Range(50, 100, ErrorMessage = "حد المشاهدة بين 50 و100")]
        public int? MinWatchPercent { get; set; }
    }

    public sealed class AddRemedialAxisInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "الخطة غير صالحة")] public int TrackId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "اختر المحور")] public int SectionId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "اختر نموذج الاختبار الأول")] public int Exam101ModelId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "اختر نموذج الاختبار الثاني")] public int Exam102ModelId { get; set; }
        [Range(5, 180, ErrorMessage = "مدة الاختبار بين 5 و180 دقيقة")] public int ExamDurationMinutes { get; set; } = 30;
    }

    public sealed class SaveRemedialAxisExamsInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "المحور غير صالح")] public int AxisId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "اختر نموذج الاختبار الأول")] public int Exam101ModelId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "اختر نموذج الاختبار الثاني")] public int Exam102ModelId { get; set; }
        [Range(5, 180, ErrorMessage = "مدة الاختبار بين 5 و180 دقيقة")] public int ExamDurationMinutes { get; set; } = 30;
    }

    public sealed class AddRemedialVideoInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "المحور غير صالح")] public int AxisId { get; set; }

        [Required(ErrorMessage = "عنوان الفيديو مطلوب")]
        [StringLength(200, ErrorMessage = "عنوان الفيديو أطول من 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "رابط الفيديو مطلوب")]
        [StringLength(500, ErrorMessage = "رابط الفيديو أطول من 500 حرف")]
        public string Url { get; set; } = string.Empty;

        [Range(10, 86400, ErrorMessage = "المدة بين 10 ثوانٍ و24 ساعة")]
        public int? DurationSeconds { get; set; }
    }

    public sealed class EditRemedialVideoInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "الفيديو غير صالح")] public int VideoId { get; set; }

        [Required(ErrorMessage = "عنوان الفيديو مطلوب")]
        [StringLength(200, ErrorMessage = "عنوان الفيديو أطول من 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "رابط الفيديو مطلوب")]
        [StringLength(500, ErrorMessage = "رابط الفيديو أطول من 500 حرف")]
        public string Url { get; set; } = string.Empty;

        [Range(10, 86400, ErrorMessage = "المدة بين 10 ثوانٍ و24 ساعة")]
        public int? DurationSeconds { get; set; }
    }

    // ============ فلتر وقوائم ============

    public sealed class RemedialTrackIndexFilter
    {
        public string? Search { get; set; }
        public int? CurriculumId { get; set; }
        public RemedialTrackStatus? Status { get; set; }
        public int Page { get; set; } = 1;
    }

    public sealed class RemedialTrackSelectOption
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? CurriculumId { get; set; }
    }

    public sealed class RemedialTrackListItemVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public RemedialTrackStatus Status { get; set; }
        public bool IsStructureLocked { get; set; }
        public int AxesCount { get; set; }
        public int VideosCount { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>عدد الطلاب المسجَّلين في الخطة (عبر كل أوامر النشر، بلا الملغاة).</summary>
        public int EnrolledCount { get; set; }
        /// <summary>عدد من بدأوا فعلًا من المسجَّلين.</summary>
        public int StartedCount { get; set; }
    }

    public sealed class RemedialTrackCurriculumTabVm
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class RemedialTrackIndexVm
    {
        public const int PageSize = 20;

        /// <summary>تابات المناهج التي لها خطط (يُحسب في الخادم).</summary>
        public List<RemedialTrackCurriculumTabVm> CurriculumTabs { get; set; } = new();

        public List<RemedialTrackListItemVm> Items { get; set; } = new();
        public List<RemedialTrackSelectOption> Curricula { get; set; } = new();
        public string? Search { get; set; }
        public int? CurriculumId { get; set; }
        public RemedialTrackStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);

        // صلاحيات العرض (تُحدَّد في الكنترولر)
        public bool CanCreate { get; set; }
    }

    public sealed class RemedialTrackCreateVm
    {
        public CreateRemedialTrackInput Input { get; set; } = new();
        public List<RemedialTrackSelectOption> Curricula { get; set; } = new();
    }

    // ============ صفحة البناء ============

    public sealed class RemedialTrackVideoVm
    {
        public int Id { get; set; }
        public int AxisId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public RemedialTrackVideoProvider Provider { get; set; }
        public string? ExternalId { get; set; }
        public int? DurationSeconds { get; set; }
        public bool IsActive { get; set; }

        /// <summary>رابط embed مبني من ExternalId فقط (آمن). null لمنصة Other.</summary>
        public string? EmbedUrl { get; set; }
    }

    public sealed class RemedialTrackAxisVm
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string? TitleOverride { get; set; }
        public string DisplayTitle => string.IsNullOrWhiteSpace(TitleOverride) ? SectionTitle : TitleOverride!;
        public int Order { get; set; }
        public int Exam101ModelId { get; set; }
        public string Exam101Title { get; set; } = string.Empty;
        public int Exam102ModelId { get; set; }
        public string Exam102Title { get; set; } = string.Empty;
        public int ExamDurationMinutes { get; set; }
        public List<RemedialTrackVideoVm> Videos { get; set; } = new();
    }

    public sealed class RemedialTrackBuilderVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
        public int PassPercent { get; set; }
        public int MinWatchPercent { get; set; }
        public RemedialTrackStatus Status { get; set; }
        public bool IsStructureLocked { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public List<RemedialTrackAxisVm> Axes { get; set; } = new();
        public List<RemedialTrackSelectOption> AvailableSections { get; set; } = new();
        public List<RemedialTrackSelectOption> Curricula { get; set; } = new();
        public List<RemedialTrackSelectOption> ExamModels { get; set; } = new();

        // صلاحيات العرض (تُحدَّد في الكنترولر)
        public bool CanEdit { get; set; }
        public bool CanCreate { get; set; }
        public bool CanArchive { get; set; }
        public bool CanPublish { get; set; }   // RTK-S3: RemedialTrackPublications:Publish

        public bool IsArchived => Status == RemedialTrackStatus.Archived;
        public bool CanModifyStructure => CanEdit && !IsStructureLocked && !IsArchived;
        public bool CanModifyContent => CanEdit && !IsArchived;
    }
}
