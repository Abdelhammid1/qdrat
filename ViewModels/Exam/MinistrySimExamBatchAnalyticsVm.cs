using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // Sprint 15 (MSE-I / I1): لوحة تحليلات مجمّعة لدفعة كاملة على اختبار محاكاة الوزارة — مستنسخة من نمط
    // ExamBatchAnalyticsViewModel (ViewModels/Exam/ExamBatchAnalyticsViewModel.cs) مع تفصيل خاص كمي/لفظي.
    // StageStats يبقى فارغًا في هذا الـSprint — تفصيل المراحل الخمس مؤجَّل لـSprint 16 (I3/I4).
    public class MinistrySimExamBatchAnalyticsVm
    {
        public int MinistrySimExamId { get; set; }
        public string ExamTitle { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public int TotalStudents { get; set; }
        public int CompletedCount { get; set; }
        public int InProgressCount { get; set; }
        public int NotStartedCount { get; set; }
        public double AvgScorePercent { get; set; }
        public double AvgQuantPercent { get; set; }
        public double AvgVerbalPercent { get; set; }
        public List<MinistrySimExamStudentSummaryVm> Students { get; set; } = new();
        public List<MinistrySimExamBatchStageStatVm> StageStats { get; set; } = new();
    }

    // Sprint 16 (MSE-I / I3) شكله النهائي — مُعرَّف هنا مسبقًا فقط لأن StageStats يحتاج النوع، القائمة نفسها لا تُملأ قبل I3.
    public class MinistrySimExamBatchStageStatVm
    {
        public int StageNumber { get; set; }
        public double AvgScorePercent { get; set; }
        public int TimeExpiredCount { get; set; }
        public int CompletedWithinTimeCount { get; set; }
    }

    public class MinistrySimExamStudentSummaryVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public bool IsCompleted { get; set; }
        public double? ScorePercent { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedAnswers { get; set; }
    }
}
