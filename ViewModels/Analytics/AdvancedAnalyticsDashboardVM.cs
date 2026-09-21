using QdratNew.ViewModels.Analytics;

namespace QdratNew.ViewModels.Admin.Analytics
{
    public class AdvancedAnalyticsDashboardVM
    {
        public int TotalBatches { get; set; }
        public int TotalInstructors { get; set; }
        public int HighRiskCount { get; set; }
        public double AvgWeakness { get; set; }

        public List<LessonGroupVM> LessonGroups { get; set; } = new();

        public List<BatchAnalyticsVM> Batches { get; set; } = new();
        public List<InstructorAnalyticsVM> Instructors { get; set; } = new();
        public List<LessonAnalyticsVM> Lessons { get; set; } = new();
        public List<StudentAnalyticsVM> Students { get; set; } = new();

        public int RiskHigh { get; set; }
        public int RiskMedium { get; set; }
        public int RiskLow { get; set; }

        public List<string> InstructorNames { get; set; } = new();
        public List<double> InstructorScores { get; set; } = new();

        public int ExcellentStudents { get; set; }
        public int MediumStudents { get; set; }
        public int WeakStudents { get; set; }
        public int WeakStudentsCount { get; set; }

        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }

        public double AvgExamScore { get; set; }
        public double AvgHomeworkScore { get; set; }
        public double AvgAttendance { get; set; }

        public int CriticalCases { get; set; }

        public int? SelectedCurriculumId { get; set; }
        public int? SelectedBatchId { get; set; }
        public int? SelectedInstructorId { get; set; }

        public List<AnalyticsFilterOptionVM> CurriculumsFilter { get; set; } = new();
        public List<AnalyticsFilterOptionVM> BatchesFilter { get; set; } = new();
        public List<AnalyticsFilterOptionVM> InstructorsFilter { get; set; } = new();

        public Dictionary<int, BatchDecisionStatusVM> DecisionStatusMap { get; set; } = new();
    }

    public class AnalyticsFilterOptionVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class StudentAnalyticsVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public double ExamScore { get; set; }
        public double HomeworkScore { get; set; }
        public double Attendance { get; set; }
        public string WeakReason { get; set; } = "";
        public string ActionRequired { get; set; } = "";
        public string Priority { get; set; } = "";
        public int? BatchId { get; set; }
    }
}