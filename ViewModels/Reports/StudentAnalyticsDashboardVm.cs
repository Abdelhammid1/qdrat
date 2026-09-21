using QdratNew.ViewModels.Exam;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Reports
{
    public class StudentAnalyticsDashboardVm
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

        // 🧠 توصيات ذكية
        public string SpeedLabel { get; set; }
        public string SpeedNote { get; set; }
        public string TrackCard1 { get; set; }
        public string TrackCard1Desc { get; set; }
        public string TrackCard2 { get; set; }
        public string TrackCard2Desc { get; set; }
        public List<string> IndividualTips { get; set; } = new();

        // 🧩 تحليل المحاور
        public List<SectionPerformanceDetailedVm> SectionPerformances { get; set; } = new();

        // ⏱️ تطور الأداء الزمني
        public List<PerformanceTimelineVm> ProgressTimeline { get; set; } = new();



        // 🧩 المناهج المجمعة (المطلوبة للفيو)
        public List<CurriculumPerformanceVm> CurriculumsGrouped { get; set; } = new();


    }


    // 🟣 المحاور داخل المنهج

}
