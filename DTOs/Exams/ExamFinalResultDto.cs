namespace QdratNew.DTOs.Exams
{
    public class ExamFinalResultDto
    {
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public double ScorePercent { get; set; }
        public int TotalTimeSeconds { get; set; }

        public Dictionary<int, SectionResultDto> Sections { get; set; }
            = new Dictionary<int, SectionResultDto>();
    }

}
