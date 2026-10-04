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
        Exam102 = 102
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
        ReportNoteSaved     = 14
    }
}
