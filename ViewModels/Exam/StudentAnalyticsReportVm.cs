using QdratNew.ViewModels.Reports;

namespace QdratNew.ViewModels.Exam
{
    public class StudentAnalyticsReportVm
    {

        public string StudentName { get; set; }
        public string School { get; set; }
        public string Level { get; set; }
        public string BatchName { get; set; }

        public int TotalExams { get; set; }
        public double AverageExamScore { get; set; }
        public string TopExamTitle { get; set; }
        public string WeakExamTitle { get; set; }

        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public double AverageHomeworkScore { get; set; }

        public double AttendancePercent { get; set; }

        public List<SectionPerformanceVm> SectionsPerformance { get; set; } = new();
        public List<CurriculumPerformanceVm> CurriculumsGrouped { get; set; } = new();
        public List<PerformanceTimelineVm> ProgressTimeline { get; set; } = new();






        // تحليل المحاور

        // تحليل المؤشرات
        public List<LessonPerformanceVm> LessonsPerformance { get; set; } = new();

        // تطور الأداء الزمني







        public string CurriculumTitle { get; set; }



    





    }

    public class PerformanceTimelineVm
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public DateTime Date { get; set; }
        public double Score { get; set; }
    }




}
