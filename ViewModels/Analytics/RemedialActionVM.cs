namespace QdratNew.ViewModels.Analytics
{
    public class RemedialActionVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public double WeakPercentage { get; set; }

        public string ActionType { get; set; } // ReExplain / Homework / Quiz

        public string Priority { get; set; } // High / Medium / Low

        public string Reason { get; set; }
    }
}