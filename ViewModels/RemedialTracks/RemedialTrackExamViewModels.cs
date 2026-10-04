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
        AwaitAdminFinal = 5      // 102 دون الحد وكان آخر محور
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
        public ResultTone Tone { get; set; } = new();
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
