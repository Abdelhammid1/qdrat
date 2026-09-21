using System.Collections.Generic;

namespace QdratNew.ViewModels.Question
{
    public class QuestionAIDashboardViewModel
    {
        public int TotalQuestions { get; set; }
        public int ReviewedQuestions { get; set; }
        public int UnansweredQuestions { get; set; }

        public Dictionary<string, int> QuestionsPerDifficulty { get; set; }
        public Dictionary<string, int> QuestionsPerCurriculum { get; set; }

        public List<MonthlyQuestionStat> QuestionsOverTime { get; set; }
        public List<AIQuestionInsight> SmartInsights { get; set; }
    }
}
