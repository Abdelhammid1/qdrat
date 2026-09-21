using System;

namespace QdratNew.ViewModels.Parents
{
    public class ParentSmartPracticeDetailsViewModel
    {
        public int RequestId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string PracticeMode { get; set; } = string.Empty;
        public string PracticeModeLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "secondary";
        public int QuestionCount { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SafeSummary { get; set; } = string.Empty;
        public bool HasResult { get; set; }
        public double? ResultScore { get; set; }
        public string? ResultTrend { get; set; }
        public string? SafeResultSummary { get; set; }
    }

    public class ParentSmartPracticeResultViewModel
    {
        public int RequestId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? GeneratedExamId { get; set; }
        public string SafeSummary { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }
}
