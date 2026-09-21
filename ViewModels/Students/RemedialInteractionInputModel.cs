namespace QdratNew.ViewModels.Students
{
    public class RemedialInteractionInputModel
    {
        public int StudentId { get; set; }

        public string ActionTaken { get; set; } = string.Empty;

        public string ActionType { get; set; } = "General"; // ✅ مهم علشان تتفادى null

        public string? Comment { get; set; }

        public int? RemedialPlanId { get; set; }
    }

}
