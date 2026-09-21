namespace QdratNew.ViewModels.Parents
{
    public class ParentChildCardViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? Level { get; set; }
        public string? School { get; set; }
        public string OverallStatus { get; set; } = "مستقر";
        public string StatusColor { get; set; } = "success";
        public string? LastActivityDate { get; set; }
        public string LearningTrend { get; set; } = "ثابت";
        public int? CommitmentPercent { get; set; }
    }
}
