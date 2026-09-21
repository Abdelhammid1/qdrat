using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels
{
    public class StudentHomeworkCardViewModel
    {
        public string CurriculumTitle { get; set; } = "—";
        public string SectionTitle { get; set; } = "—";
        public string LessonTitle { get; set; } = "—";
        public DateTime AssignedAt { get; set; }
        public int QuestionCount { get; set; }
        public int HomeworkSetId { get; set; }
        public bool IsCompleted { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public int QuestionsCount { get; set; }
        public string InstructorName { get; set; }
        public List<HomeworkLessonViewModel> Lessons { get; set; }

    }

}
