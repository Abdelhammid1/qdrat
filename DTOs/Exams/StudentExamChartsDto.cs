namespace QdratNew.DTOs.Exams
{
    public class StudentExamChartsDto
    {
        public List<string> SectionLabels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();

        public List<string> ExamTitles { get; set; } = new();
        public List<int> CorrectAnswers { get; set; } = new();
        public List<int> RemainingQuestions { get; set; } = new();

        public List<double> StudentExamScores { get; set; } = new();
        public List<double> BatchExamScores { get; set; } = new();
    }

}
