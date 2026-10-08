using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum RemedialTrackStatus
    {
        [Display(Name = "مسودة")]   Draft    = 0,
        [Display(Name = "جاهزة")]   Ready    = 1,   // اجتازت التحقق، قابلة للنشر
        [Display(Name = "مؤرشفة")]  Archived = 2
    }

    public enum RemedialTrackDeliveryMode
    {
        [Display(Name = "أونلاين")] Online   = 1,
        [Display(Name = "حضوري")]   InPerson = 2
    }

    public enum RemedialTrackPublicationScope
    {
        [Display(Name = "الدفعة كاملة")]   WholeBatch       = 1,
        [Display(Name = "طلاب محددون")]    SelectedStudents = 2
    }

    public enum RemedialTrackPublicationStatus
    {
        [Display(Name = "نشط")]    Active    = 1,   // القيمة 1 تُستخدم في الفهرس الفريد المُصفّى — لا تغيّرها
        [Display(Name = "ملغى")]   Cancelled = 2,
        [Display(Name = "مغلق")]   Closed    = 3
    }

    public enum RemedialTrackEnrollmentStatus
    {
        [Display(Name = "لم تبدأ")]            NotStarted            = 0,
        [Display(Name = "قيد التنفيذ")]        InProgress            = 1,
        [Display(Name = "مكتملة")]             Completed             = 2,
        [Display(Name = "مكتملة مع تعثّر")]    CompletedWithFailures = 3,
        [Display(Name = "ملغاة")]              Cancelled             = 4
    }

    public enum RemedialTrackAxisStatus
    {
        [Display(Name = "مغلق")]                          Locked              = 0,
        [Display(Name = "مشاهدة الفيديوهات")]             Videos              = 1,   // الجولة 1
        [Display(Name = "بانتظار اختبار 101")]            AwaitingExam101     = 2,
        [Display(Name = "إعادة المشاهدة")]                Rewatch             = 3,   // الجولة 2
        [Display(Name = "بانتظار اختبار 102")]            AwaitingExam102     = 4,
        [Display(Name = "اجتاز")]                         Passed              = 5,
        [Display(Name = "لم يجتز — بانتظار الإدارة")]     FailedBlocked       = 6,
        [Display(Name = "لم يجتز — فتحت الإدارة التالي")] FailedOpenedByAdmin = 7
    }

    public enum RemedialTrackExamNumber
    {
        Exam101 = 101,
        Exam102 = 102,
        Addendum = 3   // RTK-S13: اختبار «الملحق» — لا يمر على آلة حالة المحور أبدًا (D29)
    }

    /// <summary>الاسم المعروض للاختبار في الواجهات (الاختبار الأول / الاختبار الثاني / اختبار الملحق) — القيم داخلية فقط.</summary>
    public static class RemedialTrackExamNumberExtensions
    {
        public static string DisplayName(this RemedialTrackExamNumber number) => number switch
        {
            RemedialTrackExamNumber.Exam101 => "الاختبار الأول",
            RemedialTrackExamNumber.Addendum => "اختبار الملحق",
            _ => "الاختبار الثاني"
        };
    }

    public enum RemedialTrackAttemptStatus
    {
        InProgress = 0,
        Submitted  = 1,
        Expired    = 2   // انتهى الوقت وصُحّح تلقائيًا بما أُجيب
    }

    public enum RemedialTrackVideoProvider
    {
        YouTube = 1,
        Vimeo   = 2,
        Other   = 9
    }

    // أحداث على مستوى الطالب فقط. أحداث مستوى أمر النشر (إنشاء/إلغاء/تجديد الرقم)
    // تُسجَّل في AdminActivityLog عبر IAdminActivityLogger الحالي — لا في هذا الجدول.
    public enum RemedialTrackEventType
    {
        StudentEnrolled     = 1,
        CodeVerified        = 2,
        CodeFailed          = 3,
        VideoCompleted      = 4,
        AxisOpened          = 5,
        ExamStarted         = 6,
        ExamPassed          = 7,
        ExamFailed          = 8,
        AxisRewatchOpened   = 9,
        AxisNotPassed       = 10,   // رسب في 101 و102 — "لم يجتز الخطة العلاجية للمحور X"
        AdminOpenedNext     = 11,
        TrackCompleted      = 12,
        EnrollmentCancelled = 13,
        ReportNoteSaved     = 14,
        TermsAccepted       = 15
    }

    // ===== RTK-S11 (D23/D28): تقارير ولي الأمر =====

    public enum RemedialTrackParentReportKind
    {
        [Display(Name = "عدم اجتياز محور")] AxisNotPassed = 1,
        [Display(Name = "تقرير ختامي")]     Final         = 2
    }

    public enum RemedialTrackParentReportStatus
    {
        [Display(Name = "بانتظار الإرسال")]        Pending    = 0,
        [Display(Name = "أُرسل")]                  Sent       = 1,
        [Display(Name = "لا يوجد ولي أمر مرتبط")]  NoParent   = 2,
        [Display(Name = "موقوف")]                  Suppressed = 3
    }

    /// <summary>مسار الطالب داخل المحور (للتقرير والتوصية).</summary>
    public enum RemedialTrackPathKind
    {
        NotStarted         = 0,
        InProgress         = 1,
        PassedFirstExam    = 2,
        PassedAfterRewatch = 3,
        FailedBoth         = 4
    }
}
