using System;

namespace QdratNew.Entities
{
    public class StudentHomeworkAnalytics
    {
        public int Id { get; set; }

        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }

        public double Score { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedAnswers { get; set; }

        public double AvgTimePerQuestion { get; set; }
        public double TotalTimeSeconds { get; set; }

        public int BehaviorLevel { get; set; }

        public string SectionsJson { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}