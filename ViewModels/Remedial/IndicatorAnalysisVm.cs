namespace QdratNew.ViewModels.Remedial
{
    public class IndicatorAnalysisVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<IndicatorLessonAnalysisVm> Lessons { get; set; } = new();
    }

    public class IndicatorLessonAnalysisVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public int QuestionsInIndicatorExam { get; set; }
        public int HomeworkErrors { get; set; }
        public int ExamErrors { get; set; }
        public double IndicatorScore { get; set; }

        public string Recommendation { get; set; }
        public double HomeworkAverage { get; internal set; }
        public double ExamAverage { get; internal set; }

     
        public double ScorePercent { get; set; }

        public List<VideoOptionVm> Videos { get; set; } = new();
    }
}
