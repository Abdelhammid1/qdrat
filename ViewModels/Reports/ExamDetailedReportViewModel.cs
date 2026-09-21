using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Reports
{
    public class ExamDetailedReportViewModel
    {
        // ============================
        // 👤 بيانات الطالب
        // ============================
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? Level { get; set; }

        // ============================
        // 📝 بيانات الاختبار
        // ============================
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public DateTime ExamDate { get; set; }
        public bool IsIndividual { get; set; }

        // ============================
        // 📊 الإحصاءات العامة (الكروت)
        // ============================
        public int TotalQuestions { get; set; }
        public int TotalCorrect { get; set; }
        public int TotalWrong { get; set; }
        public int TotalSkipped { get; set; }

        public int TotalScore { get; set; }     // = TotalCorrect
        public int MaxScore { get; set; }        // = TotalQuestions

        public double OverallPercent { get; set; }
        public double SolveMinutes { get; set; }
        public double TotalMinutes { get; set; }
        public double PercentTime { get; set; }

        // ============================
        // 🎯 التحليل حسب المحاور
        // ============================
        public List<ExamSectionPerformancesVm> SectionPerformances { get; set; } = new();

        // ============================
        // 📚 التحليل حسب المؤشرات (الدروس)
        // ============================
        public List<LessonPerformancesVm> LessonPerformances { get; set; } = new();

        // ============================
        // ❌ الأسئلة الخاطئة
        // ============================
        public List<WrongQuestionVm> WrongQuestions { get; set; } = new();

        // ============================
        // 📈 الرسوم البيانية
        // ============================
        public bool HasQuantChart { get; set; }
        public bool HasVerbalChart { get; set; }

        public List<string> QuantLabels { get; set; } = new();
        public List<int> QuantCorrectCounts { get; set; } = new();
        public List<int> QuantWrongCounts { get; set; } = new();

        public List<string> VerbalLabels { get; set; } = new();
        public List<int> VerbalCorrectCounts { get; set; } = new();
        public List<int> VerbalWrongCounts { get; set; } = new();

        // ============================
        // 🤖 توصيات الذكاء الاصطناعي
        // ============================
        public string? SpeedLabel { get; set; }
        public string? SpeedNote { get; set; }
        public string? TrackCard1 { get; set; }
        public string? TrackCard1Desc { get; set; }
        public string? TrackCard2 { get; set; }
        public string? TrackCard2Desc { get; set; }
        public List<string> IndividualTips { get; set; } = new();


        // لدعم الصفحات القديمة (Backward Compatibility)
        public string? ParentPhone { get; set; }
        public string? ParentName { get; set; }

        // نفس CorrectAnswers القديمة
        public int CorrectAnswers => TotalCorrect;
        public int WrongAnswers => TotalWrong;
        public int SkippedQuestions => TotalSkipped;
        public int AnsweredQuestions => (TotalCorrect + TotalWrong);

    }


    // ============================
    // 🟥 كيانات فرعية
    // ============================

    public class ExamSectionPerformancesVm
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int Skipped { get; set; }
        public double AccuracyPercent { get; set; }
    }

    public class LessonPerformancesVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int Skipped { get; set; }
        public double AccuracyPercent { get; set; }
    }

    public class WrongQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = "";
        public string StudentAnswer { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public bool IsQuantitative { get; set; }
        public string SectionTitle { get; set; } = "";
        public string LessonTitle { get; set; } = "";
    }
}
