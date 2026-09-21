namespace QdratNew.ViewModels.Instructors.Exam
{
    public class AddQuestionVM
    {
        public int DraftId { get; set; }
        public int SectionId { get; set; }
        public string? Search { get; set; }
        public List<LessonFilterVM> Lessons { get; set; } = new();
        public int? SelectedLessonId { get; set; }
        public List<QuestionItemVM> Questions { get; set; } = new();
        public List<Guid> ExistingQuestionIds { get; set; } = new(); // 🔥 لمنع التكرار
        public int CurrentPage { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
        public int TotalCountAll { get; set; }
        public int? SelectedDifficulty { get; set; }


    }
    public class LessonFilterVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class QuestionItemVM
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string? InternalNote { get; set; }
    }
}