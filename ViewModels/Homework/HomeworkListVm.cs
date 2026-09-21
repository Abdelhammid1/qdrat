namespace QdratNew.ViewModels.Homework
{
    public class HomeworkListVm
    {
        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }

        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSubmitted { get; set; }
        public string DelayLevel { get; set; }
        public string SectionNames { get; set; }

        // 🔹 مضاف حديثاً
        public string SectionName { get; set; } = string.Empty;   // اسم المحور المرتبط بالواجب
        public double Score { get; set; } = 0;                   // درجة الطالب (٪)
        public double BatchAverageScore { get; set; } = 0;       // المتوسط الوزني للدفعة
    }
}
