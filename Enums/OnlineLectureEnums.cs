using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    // OLC-S1 — العقد الموحَّد v1.1 (C.3): الأرقام ثابتة ولا تتغير — مطابقة حرفيًا لخطة OLN في المشروع الجديد.
    // أي تعديل هنا = رفع إصدار العقد في الخطتين معًا (حارس العقد في QdratNew.Tests يفشل فورًا).

    public enum LectureContentMode
    {
        [Display(Name = "فيديو كامل")]               FullVideo       = 1,
        [Display(Name = "فيديوهات المؤشرات")]        IndicatorVideos = 2
    }

    public enum LectureContentStatus
    {
        [Display(Name = "مسودة")]   Draft     = 1,
        [Display(Name = "منشور")]   Published = 2,
        [Display(Name = "مخفي")]    Hidden    = 3
    }

    /// <summary>
    /// الأرقام مطابقة لـ VideoProvider في المشروع الجديد (YouTubeEmbed=3, VimeoEmbed=4).
    /// لا تستخدم RemedialTrackVideoProvider القديم (1/2/9).
    /// </summary>
    public enum LectureVideoProvider
    {
        [Display(Name = "YouTube")] YouTube = 3,
        [Display(Name = "Vimeo")]   Vimeo   = 4
    }

    public enum LectureContentSourceKind
    {
        [Display(Name = "رابط يدوي")]                 Manual                 = 0,
        [Display(Name = "فيديوهات المؤشرات (RemedialVideos)")] RemedialVideo  = 1,
        [Display(Name = "فيديو شرح المؤشر (المشروع الجديد)")]  LessonExplanationVideo = 2
    }
}
