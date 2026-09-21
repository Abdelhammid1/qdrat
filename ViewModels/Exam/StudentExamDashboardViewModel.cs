using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Curriculum;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels.Exam
{
    public class StudentExamDashboardViewModel
    {

        // =========================
        // كروت الإحصائيات العلوية
        // =========================
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public int Pending { get; set; }
        public int Late { get; set; }

        public int WeaknessCount { get; set; }
        public int MistakesCount { get; set; }

        // =========================
        // ترتيب الطالب
        // =========================
        public StudentRankViewModel? StudentRank { get; set; }

        // =========================
        // فلاتر المناهج
        // =========================
        // ✅ أعدها كما كانت
        public List<CurriculumVm> Curriculums { get; set; }
            = new List<CurriculumVm>();


        public List<CurriculumFilterViewModel> Curriculumes { get; set; }
        = new List<CurriculumFilterViewModel>();
        // =========================
        // التوصيات
        // =========================
        public List<string> Recommendations { get; set; }
            = new List<string>();


        // ===============================
        // Summary Cards
        // ===============================
    
        public int ExpiringSoon { get; set; }

        // ===============================
        // Rank
        // ===============================
   

        // ===============================
        // 📊 Exam Progress Charts
        // ===============================

        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedAnswers { get; set; }





        public List<string> ExamTitles { get; set; } = new();
        public List<double> StudentExamScores { get; set; } = new();
        public List<double> BatchExamScores { get; set; } = new();

        public List<int> RemainingQuestions { get; set; } = new();

        // ===============================
        // 📈 Radar Chart – Sections
        // ===============================
        public List<string> SectionLabels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();

        // ===============================
        // 💡 Recommendations
        // ===============================
       

       

        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }
        public double AverageScore { get; set; }


        public List<ExamComparisonEntry> ExamComparisons { get; set; } = new();
        public ExamRecommendationVm Recommendation { get; set; } = new ExamRecommendationVm();


        // 🔹 تحليلات ذكية
        public List<WeaknessAreaVm> WeaknessAreas { get; set; } = new();
        public PerformanceAnalysisVm? PerformanceAnalysis { get; set; }
     
        public int CurrentRank { get;  set; }
        public int TotalStudents { get;  set; }
        public double AccuracyPercent { get;  set; }
        public int TotalQuestions { get;  set; }
       
    }
}
