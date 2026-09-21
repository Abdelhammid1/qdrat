namespace QdratNew.ViewModels.Instructor.ExamDraft
{
    public class ExamDraftPreviewVM
    {
        public int DraftId { get; set; }

        public string Title { get; set; }

        public string CurriculumName { get; set; }

        public int TotalQuestions { get; set; }

        public List<ExamLessonGroupVM> LessonGroups { get; set; }
    }
}
