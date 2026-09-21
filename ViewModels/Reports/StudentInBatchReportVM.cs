using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Reports
{
    public class StudentInBatchReportVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public double HomeworkCompletionRate { get; set; }
        public double ExamCompletionRate { get; set; }

        public double AverageHomeworkScore { get; set; }
        public double AverageExamScore { get; set; }

        public List<StudentHomeworkSummaryVM> Homeworks { get; set; }
            = new();

        public List<StudentExamSummaryVM> Exams { get; set; }
            = new();
    }

    public class StudentHomeworkSummaryVM
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; }

        public bool IsSubmitted { get; set; }
        public bool IsLate { get; set; }

        public double? Score { get; set; }
    }

    public class StudentExamSummaryVM
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; }

        public bool HasStarted { get; set; }
        public bool IsSubmitted { get; set; }

        public int? Score { get; set; }
    }
}
