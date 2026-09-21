using QdratNew.Entities;

namespace QdratNew.Helpers
{
    public static class AnswerAnalysisHelper
    {
        public static bool IsRandomAnswering(IEnumerable<Homework> homeworks)
        {
            return homeworks.Count(h => h.TimeSpentSeconds != null && h.TimeSpentSeconds < 5) >= homeworks.Count() * 0.7;
        }
    }

}
