namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class InterventionTaskLectureMapItemViewModel
    {
        public int LectureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public bool IsRecommendedNextLecture { get; set; }
    }
}
