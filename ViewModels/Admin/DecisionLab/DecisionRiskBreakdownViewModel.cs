namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionRiskBreakdownViewModel
    {
        public double OverallRiskScore { get; set; }
        public string OverallRiskLevel { get; set; } = string.Empty;

        public double AttendanceRiskScore { get; set; }
        public string AttendanceSignal { get; set; } = string.Empty;

        public double HomeworkRiskScore { get; set; }
        public string HomeworkSignal { get; set; } = string.Empty;

        public double ExamRiskScore { get; set; }
        public string ExamSignal { get; set; } = string.Empty;

        public double WeakLessonRiskScore { get; set; }
        public string WeakLessonSignal { get; set; } = string.Empty;

        public double QuestionRiskScore { get; set; }
        public string QuestionSignal { get; set; } = string.Empty;
    }
}
