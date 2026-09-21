namespace QdratNew.ViewModels.Exam
{
    // Sprint 3 (MSE-B): استجابة AJAX لقائمة مؤشرات محور معيّن + عدد الأسئلة المتاحة لكل صعوبة
    public class MinistrySimExamIndicatorRowVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int EasyAvailable { get; set; }
        public int MediumAvailable { get; set; }
        public int HardAvailable { get; set; }
        public int VeryHardAvailable { get; set; }
    }

    public class MinistrySimExamIndicatorsListVm
    {
        public int SectionId { get; set; }
        public List<MinistrySimExamIndicatorRowVm> Indicators { get; set; } = new();
    }
}
