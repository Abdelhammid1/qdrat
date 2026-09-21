using QdratNew.Entities;

namespace QdratNew.ViewModels.Instructor.ExamDraft
{
    public class ExamLessonGroupVM
    {
        public int LessonId { get; set; }

        public string LessonTitle { get; set; }

        public List<ExamDraftQuestionVM> Questions { get; set; }
    }
}
