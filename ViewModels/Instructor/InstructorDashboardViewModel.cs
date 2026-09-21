using QdratNew.Areas.Instructors.Controllers;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorDashboardViewModel
    {
        public string InstructorName { get; set; } = "";

        // ── KPI الرئيسية ──
        public int TotalStudents { get; set; }
        public int TotalExamsSent { get; set; }
        public int TotalHomeworks { get; set; }
        public int ActiveHomeworksCount { get; set; }
        public int OverallCompletionRate { get; set; }
        public int TotalBatches { get; set; }
        public double AverageExamScore { get; set; }
        public double HomeworkCompletionRate { get; set; }
        public double LessonCompletionRate { get; set; }

        // ── قوائم ──
        public List<DashboardExamItemVM> RecentExams { get; set; } = new();
        public List<DashboardHomeworkItemVM> RecentHomeworks { get; set; } = new();
        public List<DashboardBatchVM> Batches { get; set; } = new();
        public List<DashboardAlertVM> Alerts { get; set; } = new();
        public List<AtRiskStudentViewModel> AtRiskStudents { get; set; } = new();
        public List<BatchAnalysisViewModel> BatchInsights { get; set; } = new();
    }


    // ══════════════════════════════════════════════════════
    //  ViewModel صفحة تفاصيل الإحصاء (StatDetails)
    // ══════════════════════════════════════════════════════
    public class InstructorStatDetailsViewModel
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Value { get; set; } = "";
        public string Insight { get; set; } = "";
        public string ActionText { get; set; } = "";
        public string ActionUrl { get; set; } = "";

        public List<InstructorStatDetailsCardVM> Cards { get; set; } = new();
        public List<InstructorStatDetailsRowVM> Rows { get; set; } = new();
    }

    public class InstructorStatDetailsCardVM
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Tone { get; set; } = "info";  // success | info | accent | warning | danger
    }

    public class InstructorStatDetailsRowVM
    {
        public string Title { get; set; } = "";
        public string Meta { get; set; } = "";
        public string Value { get; set; } = "";
        public string Url { get; set; } = "";
    }




}
