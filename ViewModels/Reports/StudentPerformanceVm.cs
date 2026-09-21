namespace QdratNew.ViewModels.Reports
{
    public class StudentPerformanceVm
    {
        // 🔹 بيانات أساسية
        public int StudentId { get; set; }
        public int ExamId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }

        // 🔹 بيانات عامة للأداء
        public double TotalScore { get; set; }               // النسبة المئوية العامة
        public int TotalQuestions { get; set; }              // إجمالي الأسئلة في الاختبار
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int Unanswered { get; set; }

        // 🔹 التحليل الزمني
        public double TimeTakenMinutes { get; set; }         // الوقت الذي استغرقه الطالب
        public double ExamDurationMinutes { get; set; }      // زمن الاختبار الكلي
        public double EngagementIndex { get; set; }          // مؤشر الجدية (TimeTaken / Duration * Accuracy)

        // 🔹 التحليل التفصيلي حسب المنهج
        public List<StudentCurriculumBreakdownVm> Curriculums { get; set; } = new();

        // 🔹 حالة الطالب في الاختبار
        public bool Completed { get; set; }                  // هل أنهى الاختبار أم لا
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

     
        public double AveragePercent { get; set; }
        public bool IsPassed => AveragePercent >= 60;
        // 🔹 مؤشرات إضافية (للتقارير المتقدمة)
        public string PerformanceLevel => GetPerformanceLevel();

        private string GetPerformanceLevel()
        {
            if (TotalScore >= 85) return "متميز";
            if (TotalScore >= 70) return "جيد جدًا";
            if (TotalScore >= 50) return "متوسط";
            return "ضعيف";
        }


       
    }

    // 🔸 التحليل حسب المنهج داخل الاختبار نفسه
    public class StudentCurriculumBreakdownVm
    {
        public string CurriculumTitle { get; set; }
        public double ScorePercent { get; set; }             // نسبة النجاح في هذا المنهج
        public int Correct { get; set; }
        public int Total { get; set; }
        public double TimeMinutes { get; set; }              // الوقت الذي استغرقه في هذا المنهج
        public string StartingLevel { get; set; }

    

        // نقطة الانطلاق
    }
}
