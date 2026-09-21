using QdratNew.Entities;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ReplaceQuestionPageVM
    {
        public int DraftId { get; set; }
        public Guid OldQuestionId { get; set; }
        public int SectionId { get; set; }

        public int TotalCountAll { get; set; }
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }

        public List<LessonVM> Lessons { get; set; }
    }
}
