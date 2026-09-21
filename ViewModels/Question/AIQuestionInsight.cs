using System;

namespace QdratNew.ViewModels.Question
{
    public class AIQuestionInsight
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public float SuccessRate { get; set; }
        public double AvgTimeToSolve { get; set; }
        public string AIComment { get; set; }
    }
}
