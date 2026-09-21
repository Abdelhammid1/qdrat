namespace QdratNew.ViewModels.Reports
{
    public class RemedialStudentVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double AveragePercent { get; set; }
      
        public List<string> FailedSections { get; set; } = new();
        // ✅ حقل اختياري لتوسيع التحليل لاحقًا
        public bool NeedsRemedialSession => AveragePercent < 50;
        public string Status => AveragePercent < 50 ? "يحتاج جلسة علاجية" : "ناجح";

        // ✅ الخصائص الجديدة المطلوبة للروابط
        public int BatchId { get; set; }
        public int ExamId { get; set; }
    }
}

