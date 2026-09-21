using QdratNew.ViewModels.Exam;

namespace QdratNew.ViewModels.Reports
{
    // 🔹 التقرير التحليلي الجديد لعرض أداء الطالب بشكل شامل
    public class StudentAnalyticsDashboard2Vm
    {
        public string StudentName { get; set; }
        public string Level { get; set; }
        public string BatchName { get; set; }

        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public double AttendancePercent { get; set; }
        public double AverageExamScore { get; set; }
        public double OverallPerformance { get; set; }

        public List<DashboardCurriculumVm> CurriculumsGrouped { get; set; } = new();
        public object SpeedLabel { get; internal set; }
        public object SpeedNote { get; internal set; }
        public object TrackCard1 { get; internal set; }
        public object TrackCard1Desc { get; internal set; }
        public object TrackCard2 { get; internal set; }
        public object TrackCard2Desc { get; internal set; }
        public List<string> IndividualTips { get; set; } = new();

        public List<SectionPerformanceDetailedVm> SectionPerformances { get; internal set; }
        public List<PerformanceTimelineVm> ProgressTimeline { get; internal set; }
    }

    // 🔹 المنهج داخل التقرير
    public class DashboardCurriculumVm
    {
        public string CurriculumTitle { get; set; }
        public List<DashboardSectionVm> Sections { get; set; } = new();
    }

    // 🔹 المحور داخل المنهج
    public class DashboardSectionVm
    {
        public string SectionTitle { get; set; }
        public List<DashboardLessonVm> Lessons { get; set; } = new();
    }

    // 🔹 المؤشر / الدرس داخل المحور
    public class DashboardLessonVm
    {
        public string LessonTitle { get; set; }
        public double SuccessRate { get; set; }
        public double ExamSuccessRate { get; set; }
        public double HomeworkSuccessRate { get; set; }
        public DateTime LastActivityDate { get; set; }
    }
}
