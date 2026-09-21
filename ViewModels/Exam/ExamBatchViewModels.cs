using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class ExamBatchCardVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }
        public int TotalStudents { get; set; }
        public int TotalExams { get; set; }
        public int SentExams { get; set; }
        public int TotalAssigned { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletionPercentage { get; set; }
    }

    public class ExamBatchDetailsPageVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }
        public int TotalStudents { get; set; }
        public bool IsArchived { get; set; }
        public List<ExamCardForBatchVM> Exams { get; set; } = new();
    }

    public class ExamCardForBatchVM
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public string CurriculumTitle { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public int DurationMinutes { get; set; }
        public int TotalQuestions { get; set; }
        public bool IsSent { get; set; }
        public bool IsArchived { get; set; }
        public bool IsOnline { get; set; }
        public bool IsInLab { get; set; }
        public int TotalAssigned { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletionPercentage { get; set; }
    }
}
