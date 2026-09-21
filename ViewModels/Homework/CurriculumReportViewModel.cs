namespace QdratNew.ViewModels.Homework
{
    public class CurriculumReportViewModel
    {
        // 📌 بيانات عامة
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }

        // 📌 البيانات التحليلية
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public double Percentage { get; set; }
        public int TimeSpentMinutes { get; set; }

        public int Rank { get; set; }
        public int TotalStudents { get; set; }

        // 📊 المحاور
        public Dictionary<string, double> SectionScores { get; set; } = new();

        // 📌 أفضل وأسوأ محور
        public string BestSection { get; set; }
        public double BestSectionScore { get; set; }
        public string WorstSection { get; set; }
        public double WorstSectionScore { get; set; }

        // ⏱ أسرع وأبطأ سؤال
        public string FastestQuestionText { get; set; }
        public int? FastestQuestionIndex { get; set; }
        public double FastestQuestionTime { get; set; }
        public bool FastestQuestionCorrect { get; set; }

        public string SlowestQuestionText { get; set; }
        public int? SlowestQuestionIndex { get; set; }
        public double SlowestQuestionTime { get; set; }
        public bool SlowestQuestionCorrect { get; set; }

        // 📋 قائمة الأسئلة (مباشرة من HomeworkReportQuestionVm)
        public List<HomeworkReportQuestionVm> Questions { get; set; } = new();

        // 📋 المناهج والمحاور المتاحة للفلتر (من CurriculumViewModel)
        public List<CurriculumViewModel> Curriculums { get; set; } = new();
        public List<string> AvailableSections { get; set; } = new();

        // 📑 لو حابب تدعم PDF Charts كصور
        public string DonutChartUrl { get; set; }
        public string BarChartUrl { get; set; }

        // 📋 لبعض التوافق مع الـ View الحالي
        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }   // 🟢 أضف دي


        public List<string> AvailableLessons { get; set; }  // ← جديدة

    }
}
