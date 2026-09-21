namespace QdratNew.ViewModels.Students
{
    // شاشة قائمة اختبارات محاكاة الوزارة للطالب — نقطة الدخول الوحيدة للميزة من واجهة الطالب (لم تكن موجودة في أي Sprint سابق)
    public class MinistrySimExamDashboardItemVm
    {
        public int MinistrySimExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public bool IsStarted { get; set; }
        public bool IsCompleted { get; set; }
        public double? TotalScorePercent { get; set; }
    }
}
