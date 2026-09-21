using QdratNew.Enums;

namespace QdratNew.ViewModels.Parent
{
    public class ParentStudentReportViewModel
    {
        // Parent info
        public int ParentID { get; set; }
        public string ParentName { get; set; } = string.Empty;
        public string RelationToStudent { get; set; } = string.Empty;

        // Student info
        public int StudentID { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? StudentLevel { get; set; }
        public string? School { get; set; }
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string EnrollmentStatus { get; set; } = string.Empty;
        public string? ProfileImagePath { get; set; }

        // Homework section
        public List<PerformanceRecordVm> HomeworkRecords { get; set; } = new();
        public int HomeworkTotal => HomeworkRecords.Count;
        public double HomeworkAvgScore => HomeworkRecords.Any() ? Math.Round(HomeworkRecords.Average(r => r.Score), 1) : 0;
        public int HomeworkPassed => HomeworkRecords.Count(r => r.Score >= 50);

        // Placement exam (تحديد مستوى)
        public List<PerformanceRecordVm> PlacementExams { get; set; } = new();
        public PerformanceRecordVm? LatestPlacement => PlacementExams.OrderByDescending(r => r.ExamDate).FirstOrDefault();

        // Performance indicator exam (مؤشر الأداء)
        public List<PerformanceRecordVm> KpiExams { get; set; } = new();
        public PerformanceRecordVm? LatestKpi => KpiExams.OrderByDescending(r => r.ExamDate).FirstOrDefault();

        // General exams
        public List<PerformanceRecordVm> GeneralExams { get; set; } = new();
        public double GeneralExamsAvg => GeneralExams.Any() ? Math.Round(GeneralExams.Average(r => r.Score), 1) : 0;
    }

    public class PerformanceRecordVm
    {
        public int Id { get; set; }
        public double Score { get; set; }
        public DateTime ExamDate { get; set; }
        public string Level { get; set; } = string.Empty;
        public string? CurriculumName { get; set; }
        public string? SectionName { get; set; }
        public ExamType? ExamType { get; set; }
        public PerformanceActivityType ActivityType { get; set; }
        public string LevelBadgeClass => Score >= 90 ? "success" : Score >= 75 ? "info" : Score >= 60 ? "warning" : Score >= 50 ? "secondary" : "danger";
    }
}
