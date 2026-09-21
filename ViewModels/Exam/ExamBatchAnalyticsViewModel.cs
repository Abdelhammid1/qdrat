using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class ExamBatchAnalyticsViewModel
    {
        public int AssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalStudents { get; set; }
        public int CompletedCount { get; set; }
        public int AbsentCount { get; set; }
        public double AvgScore { get; set; }
        public double AvgDurationMinutes { get; set; }

        public List<StudentExamStatVm> Students { get; set; } = new();
        public List<ExamSectionPerformanceVm> Sections { get; set; } = new();
        public List<QuestionPerformanceVm> Questions { get; set; } = new();

        public string BestSection { get; set; }
        public string WeakestSection { get; set; }




   
   

    }

    public class StudentExamStatVm
    {
        public string FullName { get; set; }
        public bool Attended { get; set; }
        public double? Score { get; set; }
        public double DurationMinutes { get; set; }
        // 🟢 الإضافات الجديدة
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }

    }

    public class SectionPerformancesVm
    {
        public string SectionTitle { get; set; }
        public double AvgSuccessRate { get; set; }
        public int QuestionCount { get; set; }


    }

    public class QuestionPerformanceVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public double SuccessRate { get; set; }
        public string Difficulty { get; set; }


     
    }
}
