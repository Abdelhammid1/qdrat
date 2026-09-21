namespace QdratNew.ViewModels.Partner.Reports
{
    public class BatchReportVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public int TotalStudents { get; set; }

        public int TotalHomeworkSets { get; set; }
        public int TotalExamAssignments { get; set; }

        public double HomeworkCompletionRate { get; set; }
        public double ExamParticipationRate { get; set; }

        public double AverageHomeworkScore { get; set; }
        public double AverageExamScore { get; set; }
    }
}
