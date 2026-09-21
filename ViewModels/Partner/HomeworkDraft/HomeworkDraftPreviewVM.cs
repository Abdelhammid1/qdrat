using QdratNew.ViewModels.Partner.Homework;

namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class HomeworkDraftPreviewVM
    {
        public int DraftId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public int TotalQuestions { get; set; }

        public List<HomeworkDraftLessonGroupVM> LessonGroups { get; set; } = new();

    }

}
