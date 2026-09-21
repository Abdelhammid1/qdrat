namespace QdratNew.ViewModels.Homework
{
    public class HomeworkReportViewModel
    {
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public string HomeworkTitle { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime SubmittedDate { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public double Percentage { get; set; }
        public int Rank { get; set; }
        public int TotalStudents { get; set; }
        public int TimeSpentMinutes { get; set; }
        public Dictionary<string, double> SectionScores { get; set; }
        public string BestSection { get; set; }
        public string WorstSection { get; set; }
        public string FastestAnswer { get; set; }
        public string SlowestAnswer { get; set; }

        public double AverageSecondsPerQuestion { get; set; }

        public int HomeworkSetId { get; set; }
        public string Title { get; set; }          // ✅ عنوان الواجب
        public string CurriculumTitle { get; set; } // ✅ اسم المنهج/المحور
        public DateTime CreatedAt { get; set; }    // ✅ تاريخ الإنشاء

        // باقي الخصائص اللي عندك أصلًا
        public List<HomeworkReportQuestionVm> Questions { get; set; } = new();



        // ✅ روابط الصور (للـ PDF)
        public string DonutChartUrl { get; set; }
        public string BarChartUrl { get; set; }



        public double BestSectionScore { get; set; }
        public double WorstSectionScore { get; set; }

        public Guid? FastestQuestionId { get; set; }
        public double FastestQuestionTime { get; set; }
        public bool FastestQuestionCorrect { get; set; }

        public Guid? SlowestQuestionId { get; set; }
        public double SlowestQuestionTime { get; set; }
        public bool SlowestQuestionCorrect { get; set; }
        public string? FastestQuestionText { get; set; }   // ← هنا الجديد
        public string? SlowestQuestionText { get; set; }   // ← هنا الجديد
        public int? FastestQuestionIndex { get; set; }   // رقم السؤال الأسرع في الترتيب
        public int? SlowestQuestionIndex { get; set; }   // رقم السؤال الأبطأ في الترتيب

      

        public List<SectionPerformanceEntry> SectionDetails { get; set; } = new();

        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
    }
    public class SectionPerformanceEntry
    {
        public string SectionTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Score { get; set; }

        public List<LessonPerformanceEntry> LessonBreakdown { get; set; } = new();

        public int SectionId { get; set; }        // لربط الرابط لاحقًا
        public string StrongestLesson { get; set; } = "";
        public string WeakestLesson { get; set; } = "";

    }
    public class LessonPerformanceEntry
    {
        public string LessonTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Score { get; set; }
    }



}
