using QdratNew.Entities;

public interface IStudentActivityAIAnalyzer
{
    List<string> Analyze(List<StudentActivityLog> logs);
}

public class StudentActivityAIAnalyzer : IStudentActivityAIAnalyzer
{
    public List<string> Analyze(List<StudentActivityLog> logs)
    {
        var recommendations = new List<string>();

        var weakLessons = logs
            .Where(l => l.WasCorrect == false && l.Lesson != null)
            .GroupBy(l => l.Lesson.Title)
            .Where(g => g.Count() >= 3)
            .Select(g => $"📌 كررت الخطأ {g.Count()} مرات في مؤشر {g.Key}")
            .ToList();

        var lowScores = logs
            .Where(l => l.Score.HasValue && l.Score.Value < 50)
            .Select(l => $"📉 حصلت على درجة منخفضة في {l.ActivityTitle} ({l.Score}%)")
            .ToList();

        recommendations.AddRange(weakLessons);
        recommendations.AddRange(lowScores);

        return recommendations;
    }
}
