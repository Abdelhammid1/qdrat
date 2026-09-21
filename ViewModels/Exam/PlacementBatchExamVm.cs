using QdratNew.Enums;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementBatchExamVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public int ExamsCount { get; set; }
        public DateTime LastExamDate { get; set; }
    }

    public class PlacementExamOverviewVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public int TotalQuestions { get; set; }
        public int StudentsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PlacementExamStudentVm
    {
        public int ExamId { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public bool IsSubmitted { get; set; }
        public double Score { get; set; }

        // ✅ أضف هذه الخاصية (كانت ناقصة)
        public ExamStatus ExamStatus { get; set; }

        public int AssignmentId { get; set; }

        /// <summary>true = محاولة تحديد المستوى هذه موقوفة حاليًا بسبب رصد ترجمة المتصفح (Translation Guard)</summary>
        public bool IsIntegrityBlocked { get; set; }

    }

}
