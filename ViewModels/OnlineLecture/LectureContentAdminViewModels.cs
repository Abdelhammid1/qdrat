using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;
using QdratNew.Services.OnlineLecture;

namespace QdratNew.ViewModels.OnlineLecture
{
    /// <summary>OLC-S2.3 — صفحة «المحتوى التعليمي أونلاين» لمحاضرة واحدة (الأدمن).</summary>
    public class LectureContentEditVm
    {
        // ── المحاضرة ──
        public int LectureId { get; set; }
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public string SectionTitle { get; set; } = "غير محدد";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public bool BatchIsOnline { get; set; }

        // ── المحتوى (null = لم يُنشأ بعد) ──
        public int? ContentId { get; set; }
        public bool HasContent => ContentId.HasValue;
        public LectureContentMode? ContentMode { get; set; }
        public LectureContentStatus? Status { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ActiveItemsCount { get; set; }
        public LectureVideoProvider? VideoProvider { get; set; }
        /// <summary>يبنيه الخادم من المعرّف فقط (R8) — لا يُستخدم الرابط المخزَّن خامًا في iframe.</summary>
        public string? EmbedPreviewUrl { get; set; }

        // ── النماذج ──
        public LectureContentCreateVm CreateInput { get; set; } = new();
        public LectureContentUpdateVm Input { get; set; } = new();

        // ── OLC-S3: الفصول وفيديوهات المؤشرات والنشر ──
        public int SectionId { get; set; }
        public int? DurationSeconds { get; set; }
        public string? PublishedByUserId { get; set; }

        /// <summary>FullVideo: صف لكل مؤشر مقترح من محور المحاضرة (+ أي فصل محفوظ لمؤشر خارج المحور ليُرى ويُزال).</summary>
        public List<LectureContentChapterInputVm> Chapters { get; set; } = new();

        /// <summary>IndicatorVideos: قائمة التشغيل الحالية بترتيبها.</summary>
        public List<LectureContentItemRowVm> Items { get; set; } = new();

        /// <summary>مؤشرات محور المحاضرة (R7) — للفصول ولإضافة فيديو مؤشر.</summary>
        public List<LessonOptionVm> LessonOptions { get; set; } = new();

        /// <summary>فيديوهات المؤشرات النشطة (RemedialVideos) لمؤشرات المحور — الصالحة وفق R8 فقط.</summary>
        public List<IndicatorVideoOptionVm> IndicatorVideoOptions { get; set; } = new();

        public AddIndicatorVideoVm AddInput { get; set; } = new();

        /// <summary>موانع النشر (R4/R5/R7/R8) — النشر يُرفض على الخادم ما دامت غير فارغة.</summary>
        public List<string> PublishIssues { get; set; } = new();

        /// <summary>تنبيهات لا تمنع النشر (المدة، ملاحظة R15...).</summary>
        public List<string> PublishWarnings { get; set; } = new();

        public bool CanPublish => HasContent && PublishIssues.Count == 0 && Status != LectureContentStatus.Published;

        // ── يملؤها الـ Controller ──
        public bool FeatureEnabled { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        /// <summary>التبويب المفتوح بعد إعادة التوجيه: basic أو content.</summary>
        public string ActiveTab { get; set; } = "basic";
    }

    /// <summary>OLC-S3.1 — صف فصل في محرر الفصول (FullVideo). يُرسل النموذج كل الصفوف و<see cref="Include"/> يحدد المعتمد منها.</summary>
    public class LectureContentChapterInputVm
    {
        public int LessonId { get; set; }

        public bool Include { get; set; }

        [MaxLength(200, ErrorMessage = "عنوان الفصل لا يتجاوز 200 حرف")]
        public string? Title { get; set; }

        /// <summary>mm:ss أو hh:mm:ss (أرقام عربية أو هندية).</summary>
        [MaxLength(12)]
        public string? StartText { get; set; }

        [MaxLength(12)]
        public string? EndText { get; set; }

        // ── للعرض فقط (لا يُقرأ من الطلب) ──
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string LessonTitle { get; set; } = string.Empty;

        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string? UnitTitle { get; set; }

        /// <summary>فصل محفوظ لمؤشر لم يعد ضمن محور المحاضرة (R7) — يجب إزالته قبل الحفظ.</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public bool OutsideSection { get; set; }
    }

    /// <summary>OLC-S3.2 — عنصر في قائمة فيديوهات المؤشرات.</summary>
    public class LectureContentItemRowVm
    {
        public int Id { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int SortOrder { get; set; }
        public LectureContentSourceKind SourceVideoKind { get; set; }
        public int? SourceVideoId { get; set; }
        public LectureVideoProvider? VideoProvider { get; set; }
        public string? VideoUrl { get; set; }
        public int? DurationSeconds { get; set; }
        /// <summary>يبنيه الخادم من المعرّف فقط (R8).</summary>
        public string? EmbedUrl { get; set; }
        public bool OutsideSection { get; set; }
        /// <summary>RemedialVideo: المصدر تغيّر بعد أخذ اللقطة (R6) — يظهر زر «تحديث من المصدر».</summary>
        public bool SourceChanged { get; set; }
        /// <summary>RemedialVideo: المصدر حُذف أو عُطّل أو صار رابطه غير صالح — اللقطة تبقى تعمل.</summary>
        public bool SourceUnavailable { get; set; }
    }

    /// <summary>مؤشر من محور المحاضرة (Lesson.SectionId = Lecture.SectionId).</summary>
    public class LessonOptionVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? UnitTitle { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>فيديو مؤشر من RemedialVideos النشطة (رابطه صالح وفق R8).</summary>
    public class IndicatorVideoOptionVm
    {
        public int Id { get; set; }
        public int LessonId { get; set; }
        public string Title { get; set; } = string.Empty;
        public LectureVideoProvider Provider { get; set; }
    }

    /// <summary>OLC-S3.2 — إضافة فيديو مؤشر: من فيديوهات المؤشر الموجودة أو برابط يدوي.</summary>
    public class AddIndicatorVideoVm
    {
        [Required(ErrorMessage = "اختر المؤشر")]
        [Display(Name = "المؤشر")]
        public int? LessonId { get; set; }

        /// <summary>RemedialVideo أو Manual فقط (LessonExplanationVideo للمشروع الجديد).</summary>
        public LectureContentSourceKind SourceKind { get; set; } = LectureContentSourceKind.RemedialVideo;

        public int? SourceVideoId { get; set; }

        [MaxLength(500, ErrorMessage = "الرابط لا يتجاوز 500 حرف")]
        public string? VideoUrl { get; set; }

        [MaxLength(200, ErrorMessage = "العنوان لا يتجاوز 200 حرف")]
        public string? Title { get; set; }

        [MaxLength(12)]
        public string? DurationText { get; set; }
    }

    /// <summary>OLC-S2.2 — إنشاء المسودة عند أول فتح (بعد تأكيد الأدمن).</summary>
    public class LectureContentCreateVm
    {
        [Required(ErrorMessage = "عنوان المحتوى مطلوب")]
        [MaxLength(200, ErrorMessage = "العنوان لا يتجاوز 200 حرف")]
        [Display(Name = "عنوان المحتوى")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اختر وضع المحتوى")]
        [Display(Name = "وضع المحتوى")]
        public LectureContentMode? ContentMode { get; set; }
    }

    /// <summary>OLC-S2.2 — البيانات الأساسية + الوضع + الفيديو الكامل.</summary>
    public class LectureContentUpdateVm
    {
        [Required(ErrorMessage = "عنوان المحتوى مطلوب")]
        [MaxLength(200, ErrorMessage = "العنوان لا يتجاوز 200 حرف")]
        [Display(Name = "عنوان المحتوى")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "الوصف لا يتجاوز 1000 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "اختر وضع المحتوى")]
        [Display(Name = "وضع المحتوى")]
        public LectureContentMode? ContentMode { get; set; }

        /// <summary>FullVideo فقط — يُتحقق منه على الخادم (R8) ويُخزَّن بصيغة قانونية.</summary>
        [MaxLength(500, ErrorMessage = "الرابط لا يتجاوز 500 حرف")]
        [Display(Name = "رابط الفيديو الكامل")]
        public string? VideoUrl { get; set; }

        /// <summary>FullVideo فقط — mm:ss أو hh:mm:ss أو ثوانٍ.</summary>
        [MaxLength(12)]
        [Display(Name = "مدة الفيديو")]
        public string? DurationText { get; set; }

        [MaxLength(500, ErrorMessage = "الرابط لا يتجاوز 500 حرف")]
        [Display(Name = "رابط الصورة المصغّرة")]
        public string? ThumbnailUrl { get; set; }

        /// <summary>تأكيد صريح عند تغيير الوضع ووجود عناصر (تُحذف حذفًا ناعمًا).</summary>
        public bool ConfirmModeChange { get; set; }
    }

    /// <summary>OLC-S2.1 — حالة التعليم أونلاين لدفعة (لشاشتي تعديل/تفاصيل الدفعة).</summary>
    public class BatchOnlineLearningSummaryVm
    {
        public int BatchId { get; set; }
        public bool IsOnlineLearning { get; set; }
        public DateTime? OnlineLearningEnabledAt { get; set; }
        public bool IsArchived { get; set; }
        public int LecturesCount { get; set; }
        public int ContentsCount { get; set; }
        public int PublishedCount { get; set; }

        // OLC-S4.2: دفعة شريك (R16) — null = دفعة منصة
        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }
        public PartnerEducationalContentState? PartnerContractState { get; set; }
        /// <summary>سبب منع التفعيل وفق R16 (null = التفعيل مسموح).</summary>
        public string? PartnerBlockReason { get; set; }
        public bool IsPartnerBatch => PartnerId.HasValue;

        // يملؤها الـ Controller
        public bool CanToggle { get; set; }
        public bool FeatureEnabled { get; set; }
        /// <summary>الصفحة التي يعود إليها نموذج التفعيل: Edit أو Details.</summary>
        public string ReturnTo { get; set; } = "Edit";
    }

    // ═════════════════════════════ OLC-S4.2 — «الدفعات الأونلاين» ═════════════════════════════

    /// <summary>فلتر الشاشة (GET). PartnerIds تُطابق على قائمة الشركاء المحمّلة من الخادم.</summary>
    public class OnlineBatchesFilterVm
    {
        public DispatchAudience Audience { get; set; } = DispatchAudience.Platform;
        public List<int>? PartnerIds { get; set; }
        /// <summary>إظهار الدفعات غير المفعّلة أيضًا (غير المؤرشفة) للوصول لبطاقة التفعيل.</summary>
        public bool IncludeInactive { get; set; }
    }

    public class OnlineBatchesPageVm
    {
        public DispatchAudience Audience { get; set; } = DispatchAudience.Platform;
        public bool IncludeInactive { get; set; }
        public bool CanManagePartners { get; set; }
        public bool FeatureEnabled { get; set; }
        public List<int> SelectedPartnerIds { get; set; } = new();
        public List<OnlineBatchesPartnerVm> Partners { get; set; } = new();
        public List<OnlineBatchRowVm> Batches { get; set; } = new();
    }

    public class OnlineBatchesPartnerVm
    {
        public int PartnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public PartnerEducationalContentState State { get; set; }
        public int OnlineBatchesCount { get; set; }
        public bool IsSelected { get; set; }
    }

    public class OnlineBatchRowVm
    {
        public int BatchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? CourseName { get; set; }
        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }
        public bool IsOnlineLearning { get; set; }
        public DateTime? OnlineLearningEnabledAt { get; set; }
        public bool IsArchived { get; set; }
        public int ActiveStudents { get; set; }
        public int LecturesCount { get; set; }
        public int ContentsCount { get; set; }
        public int PublishedCount { get; set; }
        /// <summary>دفعة شريك فقط: حالة البند في عقده الآن.</summary>
        public PartnerEducationalContentState? ContractState { get; set; }
    }

    /// <summary>نص وشارة حالة البند — مشتركة بين الشاشات.</summary>
    public static class PartnerEducationalContentStateText
    {
        public static string Label(PartnerEducationalContentState? state) => state switch
        {
            PartnerEducationalContentState.Enabled => "البند مفعّل",
            PartnerEducationalContentState.NotInContract => "غير مفعّل في العقد",
            PartnerEducationalContentState.NoActiveContract => "لا عقد نشط",
            _ => "—"
        };

        public static string Css(PartnerEducationalContentState? state) => state switch
        {
            PartnerEducationalContentState.Enabled => "olc-badge--published",
            PartnerEducationalContentState.NotInContract => "olc-badge--draft",
            _ => "olc-badge--hidden"
        };
    }
}
