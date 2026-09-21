namespace QdratNew.ViewModels.Batch
{
    public class BatchArchiveViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CourseName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Gender { get; set; }
        public int StudentsCount { get; set; }
        public int LecturesCount { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserName { get; set; }
    }
}
