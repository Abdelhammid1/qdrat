namespace QdratNew.ViewModels.Analytics
{
    public class PerformanceInsightResult
    {
        public int StudentId { get; set; }
        public int ExamId { get; set; }

        // 🔢 التحليل العام
        public double OverallScore { get; set; }           // النسبة المئوية العامة
        public double TimeUsagePercent { get; set; }       // نسبة الوقت المستخدم
        public double TotalTimeSeconds { get; set; }       // إجمالي الزمن بالثواني

        // 🧠 التوصيات والتحليل الذكي
        public string SpeedAccuracyFeedback { get; set; }  // تحليل السرعة × الدقة
        public string ScoreBand { get; set; }              // الشريحة العامة (ضعيف – ممتاز)
        public string MotivationalMessage { get; set; }    // العبارة التحفيزية العامة

        // 📊 تحليل المحاور
        public List<SectionInsight> Sections { get; set; } = new();


        public string BatchName { get; set; }         // اسم الدفعة أو رقمها
        public double ElapsedMinutes { get; set; }    // الوقت المستغرق بالدقائق
        public string ElapsedTimeFormatted { get; set; } // الوقت بصيغة "ساعات:دقائق:ثواني"
        public int SkippedQuestions { get; internal set; }
        public string CheatingFlagMessage { get; internal set; }

        public string CheatingFlag { get; set; }

    }

    public class SectionInsight
    {
        public string SectionTitle { get; set; }   // اسم المحور
        public double Score { get; set; }          // نسبة الأداء في المحور
        public string Guidance { get; set; }

        // ✅ الحقول الجديدة الخاصة بالرسم البياني
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SectionId { get; set; }
        public int SkippedCount { get; set; }

    }
}
