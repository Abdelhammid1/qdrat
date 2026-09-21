namespace QdratNew.ViewModels.Instructor
{
    public class StudentHomeworkArchiveViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public List<HomeworkSummaryViewModel> Homeworks { get; set; }
    }
}
