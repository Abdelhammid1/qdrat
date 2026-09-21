using System;
using System.Collections.Generic;
using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkFullReportVm
    {
        // 🔹 بيانات الطالب
        public string StudentName { get; set; } = "";
        public string BatchName { get; set; } = "";

        // 🔹 بيانات الواجب
        public int HomeworkSetId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedCount { get; set; }

        // 🔹 مؤشرات الأداء العامة
        public double Percentage { get; set; }
        public double TimeSpentMinutes { get; set; }
        public int Rank { get; set; }
        public int TotalStudents { get; set; }

        // 🔹 التقييم العام (نجوم)
        public int Stars => Percentage >= 90 ? 5 :
                            Percentage >= 75 ? 4 :
                            Percentage >= 60 ? 3 :
                            Percentage >= 40 ? 2 : 1;

        // 🔹 أداء المحاور
        public Dictionary<string, double> SectionScores { get; set; } = new();
        public string BestSection { get; set; } = "";
        public double BestSectionScore { get; set; }
        public string WorstSection { get; set; } = "";
        public double WorstSectionScore { get; set; }

        // 🔹 أسرع سؤال / أبطأ سؤال
        public string FastestQuestionText { get; set; } = "";
        public int FastestQuestionIndex { get; set; }
        public double FastestQuestionTime { get; set; }
        public bool FastestQuestionCorrect { get; set; }

        public string SlowestQuestionText { get; set; } = "";
        public int SlowestQuestionIndex { get; set; }
        public double SlowestQuestionTime { get; set; }
        public bool SlowestQuestionCorrect { get; set; }

        // 🔹 روابط للرسوم البيانية (PDF Mode)
        public string? DonutChartUrl { get; set; }
        public string? BarChartUrl { get; set; }

        // 🔹 الأسئلة (قائمة مفصلة)
        public List<HomeworkQuestionResultItem> Questions { get; set; } = new();
        public List<LessonPerformanceVm> LessonPerformances { get; set; } = new();




            // 🔹 إحصائيات عامة
            public int CorrectCount { get; set; }
            public int WrongCount { get; set; }
            public double ScorePercentage { get; set; }

            // 🔹 التحليل حسب المحاور والمؤشرات
            public List<SectionPerformanceVm> SectionsPerformance { get; set; } = new();
            public List<LessonPerformanceVm> LessonsPerformance { get; set; } = new();

        

            // 🔹 ملخص أداء المحاور
            public string BestSectionName => SectionScores.Any() ? SectionScores.OrderByDescending(x => x.Value).First().Key : "غير محدد";




}
}
