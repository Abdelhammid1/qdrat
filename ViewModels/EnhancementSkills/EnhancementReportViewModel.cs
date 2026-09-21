namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementReportViewModel
    {
        public int SetId { get; set; }
        public int StudentId { get; set; }
        public string Title { get; set; } = "";
        public string? LectureTitle { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StudentName { get; set; } = "";
        public string BatchName { get; set; } = "";

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }

        public int SkippedCount => Questions.Count(q => string.IsNullOrEmpty(q.StudentAnswer));
        public int WrongCount   => TotalQuestions - CorrectAnswers - SkippedCount;

        public double ScorePercentage => TotalQuestions > 0
            ? Math.Round(CorrectAnswers * 100.0 / TotalQuestions, 1) : 0;

        public List<EnhancementReportItemViewModel> Questions { get; set; } = new();

        // أداء المحاور (يُحسب تلقائياً)
        public List<EnhancementSectionPerformance> SectionsPerformance =>
            Questions
                .GroupBy(q => q.SectionTitle ?? "غير محدد")
                .Select(g => new EnhancementSectionPerformance
                {
                    SectionTitle   = g.Key,
                    TotalQuestions = g.Count(),
                    CorrectCount   = g.Count(q => q.IsCorrect),
                    WrongCount     = g.Count(q => !q.IsCorrect && !string.IsNullOrEmpty(q.StudentAnswer)),
                    SkippedCount   = g.Count(q => string.IsNullOrEmpty(q.StudentAnswer)),
                    Accuracy       = g.Count() > 0
                        ? Math.Round(g.Count(q => q.IsCorrect) * 100.0 / g.Count(), 1) : 0
                })
                .OrderByDescending(s => s.Accuracy)
                .ToList();
    }

    public class EnhancementReportItemViewModel
    {
        public string QuestionTitle { get; set; } = "";
        public string? SectionTitle { get; set; }
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class EnhancementSectionPerformance
    {
        public string SectionTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double Accuracy { get; set; }
    }
}
