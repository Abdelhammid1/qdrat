using QdratNew.Entities;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Reports;

namespace QdratNew.ViewModels.Students
{
    public class PlacementExamResultVm
    {
        public string ExamTitle { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double Score { get; set; }
        public string TimeSpentFormatted { get; set; }
        public int TimeSpentMinutes { get; set; }
        public int WrongAnswers { get; set; }

        // 🆕 عدد إجابات "لا أعرف الإجابة" (مُتضمَّنة أصلًا داخل WrongAnswers، وتُعرض هنا كإحصائية منفصلة)
        public int DontKnowAnswers { get; set; }

        public int AnsweredQuestions { get; set; }   // ✅ الأسئلة المجاب عنها
        public int SkippedQuestions { get; set; }    // ✅ الأسئلة المتخطاة
        public List<KeyValuePair<string, double>> VerbalSectionsChart { get; set; } = new();
        public List<KeyValuePair<string, double>> QuantitativeSectionsChart { get; set; } = new();
        public List<CurriculumChartVm> CurriculumCharts { get; set; } = new();


    }

    public class PlacementExamReportVm
    {
        public string QuestionTitle { get; set; }
        public List<string> Options { get; set; }
        public string CorrectAnswer { get; set; }
        public string? StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }



        // ✅ جديدة لأسئلة المقارنة
        public string? FirstValue { get; set; }
        public string? SecondValue { get; set; }

        // 🆕 خصائص إضافية لتقرير الطالب
        public string? StudentName { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentLevel { get; set; }
        public DateTime ExamDate { get; set; }
        public int TimeTakenSeconds { get; set; }
        public string? ImageUrl { get; set; }

        // 🧮 خاصية مشتقة لحساب الوقت بالدقائق
        public int TimeTakenMinutes => (int)(TimeTakenSeconds / 60.0);

        public string ExamTitle { get; set; }

        // 🔹 عدد الأسئلة
        public int TotalQuestions { get; set; }

        // 🔹 عدد الإجابات الصحيحة
        public int CorrectAnswers { get; set; }

        // 🔹 عدد الإجابات الخاطئة
        public int WrongAnswers { get; set; }

        // 🔹 النسبة المئوية
        public double OverallPercent { get; set; }

        // 🔹 الوقت المستغرق (بالدقائق)
        public double SolveMinutes { get; set; }

        // 🔹 الوقت بصيغة نصية
        public string SolveTimeFormatted { get; set; }

        // 🔹 تاريخ الاختبار

        // 🔹 بيانات الطالب
        public Student Student { get; set; }


        // 🔹 تحليل الكمي واللفظي (اختياري)
        public List<string> QuantLabels { get; set; } = new();
        public List<double> QuantScores { get; set; } = new();

        public List<string> VerbalLabels { get; set; } = new();
        public List<double> VerbalScores { get; set; } = new();
        public List<ExamSectionPerformanceVm> SectionAnalysis { get; set; } = new();

        public PlacementRecommendationVm Recommendation { get; internal set; }
    }
}
