using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.RemedialTracks
{
    /// <summary>RTK-S5.2: سؤال في صفحة الحل. الإجابة الصحيحة/الشرح/الفيديو مصفّرة دائمًا (D15).</summary>
    public sealed class StudentRemedialTrackSolveQuestionVm
    {
        public int Index { get; set; }                       // 1..n
        public Guid QuestionId { get; set; }
        public QuestionDisplayViewModel Question { get; set; } = null!;
        public string? SelectedAnswer { get; set; }
    }

    public sealed class StudentRemedialTrackSolveVm
    {
        public int AttemptId { get; set; }
        public int EnrollmentId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public RemedialTrackExamNumber ExamNumber { get; set; }
        public int RemainingSeconds { get; set; }            // من ساعة الخادم
        public int AnsweredCount => Questions.Count(q => !string.IsNullOrWhiteSpace(q.SelectedAnswer));
        public IReadOnlyList<StudentRemedialTrackSolveQuestionVm> Questions { get; set; } = Array.Empty<StudentRemedialTrackSolveQuestionVm>();
    }

    /// <summary>جسم حفظ الإجابة (JSON).</summary>
    public sealed class RemedialTrackSaveAnswerRequest
    {
        public int AttemptId { get; set; }
        public Guid QuestionId { get; set; }
        public string? Answer { get; set; }
    }

    public sealed class RemedialTrackSaveAnswerResponse
    {
        public bool Ok { get; set; }
        public string? Reason { get; set; }                  // bad-request | expired | notfound | forbidden
        public string? Message { get; set; }
        public int RemainingSeconds { get; set; }
    }

    /// <summary>المسار التالي بعد النتيجة.</summary>
    public enum RemedialTrackResultNext
    {
        NextAxis = 1,            // اجتاز وهناك محور تالٍ
        FinalReport = 2,         // اجتاز وكان آخر محور
        RewatchThenExam102 = 3,  // 101 دون الحد ← إعادة الفيديوهات ثم 102
        AwaitAdmin = 4,          // 102 دون الحد وهناك محاور لاحقة ← بانتظار الإدارة
        AwaitAdminFinal = 5,     // 102 دون الحد وكان آخر محور
        AddendumPassed = 6,      // RTK-S13: اجتاز اختبار الملحق (لا أثر على المحاور)
        AddendumRetry = 7        // RTK-S13: دون الحد ← إعادة اختبار الملحق (محاولات غير محدودة)
    }

    /// <summary>RTK-S5.3: صفحة النتيجة — بلا كشف الإجابات الصحيحة ولا مراجعة الأسئلة.</summary>
    public sealed class StudentRemedialTrackResultVm
    {
        public int AttemptId { get; set; }
        public int EnrollmentId { get; set; }
        public int AxisProgressId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public RemedialTrackExamNumber ExamNumber { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }
        public bool TimedOut { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public int PassPercent { get; set; }
        public RemedialTrackResultNext Next { get; set; }
        public int? NextAxisProgressId { get; set; }
        public string? NextAxisTitle { get; set; }
        public int? AddendumId { get; set; }                 // RTK-S13: يُملأ لنتيجة اختبار ملحق فقط
        public ResultTone Tone { get; set; } = new();
    }

    /// <summary>أداء الطالب في قسم أو درس داخل تقرير محاولة واحدة.</summary>
    public sealed class StudentRemedialTrackAttemptBreakdownVm
    {
        public string Title { get; set; } = string.Empty;
        public string? ParentTitle { get; set; }             // عنوان القسم لسطر الدرس
        public int Total { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public double AccuracyPercent => Total <= 0 ? 0 : Math.Round(Correct * 100.0 / Total, 1);
    }

    /// <summary>سؤال خاطئ/متخطى في تقرير المحاولة (بلا نص الإجابة الصحيحة — تُعرض في صفحة المراجعة).</summary>
    public sealed class StudentRemedialTrackAttemptMissedVm
    {
        public int Order { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public bool IsSkipped { get; set; }
    }

    /// <summary>تقرير تفصيلي لمحاولة واحدة مغلقة (101 أو 102).</summary>
    public sealed class StudentRemedialTrackAttemptReportVm
    {
        public int AttemptId { get; set; }
        public int EnrollmentId { get; set; }
        public int AxisProgressId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string AxisTitle { get; set; } = string.Empty;
        public RemedialTrackExamNumber ExamNumber { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public bool TimedOut { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }
        public int PassPercent { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public int AllowedMinutes { get; set; }
        public double SolveMinutes { get; set; }
        public ResultTone Tone { get; set; } = new();
        public IReadOnlyList<StudentRemedialTrackAttemptBreakdownVm> Sections { get; set; } = Array.Empty<StudentRemedialTrackAttemptBreakdownVm>();
        public IReadOnlyList<StudentRemedialTrackAttemptBreakdownVm> Lessons { get; set; } = Array.Empty<StudentRemedialTrackAttemptBreakdownVm>();
        public IReadOnlyList<StudentRemedialTrackAttemptMissedVm> Missed { get; set; } = Array.Empty<StudentRemedialTrackAttemptMissedVm>();
    }

    /// <summary>سطر محاولة في صفحة المحور.</summary>
    public sealed class StudentRemedialTrackAttemptItemVm
    {
        public int AttemptId { get; set; }
        public RemedialTrackExamNumber ExamNumber { get; set; }
        public RemedialTrackAttemptStatus Status { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }
        public bool IsClosed => Status != RemedialTrackAttemptStatus.InProgress;
    }
}
