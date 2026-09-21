using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkAnalyticsVm
    {
        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }
        
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }

        public double ScorePercentage { get; set; }
        public List<LessonPerformanceVm> LessonsPerformance { get; set; } = new();

        public List<HomeworkQuestionResultItem> Questions { get; set; } = new();
        public List<SectionPerformanceVm> SectionsPerformance { get; set; } = new();


        // 🔹 معلومات عامة إضافية لعرض التقارير
        public string StudentName { get; set; } = string.Empty;
        public string BestSection { get; set; } = string.Empty;
        public string BestLesson { get; set; } = string.Empty;

        // 🔹 خصائص مساعدة للعرض في التقارير
        public string DonutChartUrl { get; set; } = string.Empty;
        public string BarChartUrl { get; set; } = string.Empty;

        public string? BatchName { get; set; }
        public double TimeSpentMinutes { get; set; }




        // 🧠 توصيات الذكاء الاصطناعي (بدلاً من ViewBag)
        public HomeworkRecommendationVm? Recommendation { get; set; }

        public string? TimeExplanation { get; set; }
        public int QuestionsCount { get; internal set; }
        public int CorrectAnswers { get; internal set; }
        public bool IsQuantitative { get; internal set; }
    }


    public class SectionPerformanceVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public double Accuracy { get; set; }
        public int Total { get; internal set; }
        public int Correct { get; internal set; }
        public int Wrong { get; internal set; }
        public int Skipped { get; internal set; }
        public double ScorePercentage { get; set; }
    }
}
