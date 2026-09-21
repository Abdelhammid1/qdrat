namespace QdratNew.Analytics
{
    public class StudentLearningAnalyticsDto
    {
        public int StudentId { get; set; }
        public int CurriculumId { get; set; }
        public int SectionId { get; set; }

        // 📊 Features (المدخلات للتحليل + ML)
        public double AttendanceRate { get; set; }          // نسبة حضور المحاضرات
        public double HomeworkCompletionRate { get; set; }  // نسبة تسليم الواجبات
        public double HomeworkAccuracy { get; set; }        // دقة الإجابات في الواجبات
        public double ExamAccuracy { get; set; }            // دقة الإجابات في الاختبارات
        public double EngagementScore { get; set; }         // معدل التفاعل

        // 📊 Raw performance data (تفاصيل إضافية من الإجابات)
        public int TotalAttempts { get; set; }              // عدد المحاولات الكلي
        public int CorrectAttempts { get; set; }            // عدد الإجابات الصحيحة
        public double QuantitativeAccuracy { get; set; }    // نسبة النجاح في الأسئلة الكمية
        public double VerbalAccuracy { get; set; }          // نسبة النجاح في الأسئلة اللفظية

        // 🔮 Prediction / Recommendation
        public string Recommendation { get; set; } = "";    // توصية ذكية للطالب
        public double SuccessProbability { get; set; }      // نسبة التنبؤ بالنجاح
    }
}
