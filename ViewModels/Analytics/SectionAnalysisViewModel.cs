using System.Collections.Generic;

namespace QdratNew.ViewModels.Analytics
{
    public class SectionAnalysisViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }

        public int TotalQuestions { get; set; }           // عدد الأسئلة المرتبطة بالمحور
        public int QuestionsInExams { get; set; }         // عدد الأسئلة التي ظهرت فعليًا في اختبارات
        public double AverageSuccessRate { get; set; }    // متوسط نسبة النجاح لأسئلة هذا المحور
        public double AverageDifficulty { get; set; }     // متوسط مستوى الصعوبة الفعلي بناءً على أداء الطلاب

        public string WeaknessLevel { get; set; }         // تقدير عام لمستوى الضعف في هذا المحور

        // اختياري: لتصفية البيانات بناء على دفعة معينة
        public int? BatchId { get; set; }
        public string? BatchName { get; set; }
    }
}
