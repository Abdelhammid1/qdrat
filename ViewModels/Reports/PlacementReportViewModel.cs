using System;
using System.Collections.Generic;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Reports;

namespace QdratNew.ViewModels.Reports
{
    public class PlacementReportViewModel
    {
        public StudentMiniVm Student { get; set; }
        public string ExamDate { get; set; }
        public double SolveMinutes { get; set; }
        public double OverallPercent { get; set; }
        public ExamRecommendationVm Recommendation { get; set; }

        public List<string> QuantLabels { get; set; } = new();
        public List<double> QuantScores { get; set; } = new();
        public List<string> VerbalLabels { get; set; } = new();
        public List<double> VerbalScores { get; set; } = new();

        // 🟢 الخصائص الجديدة لتحليل الأسئلة الصحيحة والخاطئة
        public List<int> QuantCorrectCounts { get; set; } = new List<int>();
        public List<int> QuantWrongCounts { get; set; } = new List<int>();
        public List<int> VerbalCorrectCounts { get; set; } = new List<int>();
        public List<int> VerbalWrongCounts { get; set; } = new List<int>();


        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }

        // 🆕 عدد إجابات "لا أعرف الإجابة" (مُتضمَّنة أصلًا داخل WrongAnswers، وتُعرض هنا كإحصائية منفصلة)
        public int DontKnowAnswers { get; set; }

        public int AnsweredQuestions { get; set; }
        public int SkippedQuestions { get; set; }



        public double AverageSecondsPerQuestion { get; set; }
        public string FastestQuestion { get; set; }
        public double FastestTimeSeconds { get; set; }
        public string SlowestQuestion { get; set; }
        public double SlowestTimeSeconds { get; set; }
        public string TimeSpentFormatted { get; set; }
        public string TimeExplanation { get; set; }


        public double TotalMinutes { get; set; }
        public double PercentTime { get; set; }


        public int AssignmentId { get; set; } // 🆕 لتضمين معرف التعيين

        public List<SectionLessonReportVm> SectionLessonReports { get; set; } = new();
        public bool HasVerbalChart { get; internal set; }
        public bool HasQuantChart { get; internal set; }
    }

    public class StudentMiniVm
    {
        public string FullName { get; set; }
        public int StudentID { get; set; }
        public string Level { get; set; }
        public string ParentName { get; set; }
        public string ParentPhone { get; set; }
    }
}
