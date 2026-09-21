using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.PerformanceIndicator
{
    public class PerformanceIndicatorResultVm
    {
        public int ExamId { get; set; }

        // 🔹 عنوان الاختبار
        public string ExamTitle { get; set; }

        // 🔹 جميع المحاور مع نتائجها
        public List<SectionResultVm> Sections { get; set; } = new();

        // 🔹 المتوسط العام للنسبة المئوية
        public double TotalAverage { get; set; }

        // 🔹 التاريخ الذي بدأ فيه الطالب الاختبار
        public DateTime AssignedAt { get; set; }

        // 🔹 التاريخ الذي سلم فيه الاختبار (إن وُجد)
        public DateTime? SubmittedAt { get; set; }

        // 🔹 عدد المحاور
        public int TotalSections => Sections?.Count ?? 0;

        // 🔹 عدد الأسئلة (اختياري، يمكن ربطه لاحقًا)
        public int TotalQuestions { get; set; }

        // 🔹 الوقت الإجمالي بالدقائق
        public double TotalTimeMinutes { get; set; }



     

        // ✅ قائمة المحاور الضعيفة
        public List<SectionResultVm> WeakSections =>
            Sections?.Where(s => s.ScorePercent < 60).ToList() ?? new List<SectionResultVm>();

        public bool HasWeakSections => WeakSections.Any();

        public double AverageTimePerQuestion { get; internal set; }
        public string CurriculumTitle { get; internal set; }
        public int CorrectAnswers { get; internal set; }
        public int WrongAnswers { get; internal set; }
        public int SkippedQuestions { get; internal set; }
        public string TimeSpentFormatted { get; internal set; }
    }

    public class SectionResultVm
    {
        public string SectionName { get; set; }
        public double ScorePercent { get; set; }
        public double AverageTime { get; set; }
    }
}
