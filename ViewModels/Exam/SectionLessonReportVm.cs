namespace QdratNew.ViewModels.Exam
{
    public class SectionLessonReportVm
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; }
        public List<LessonPerformanceVm> Lessons { get; set; }

      
    }
}
