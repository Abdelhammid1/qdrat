using QdratNew.Entities;

namespace QdratNew.ViewModels.Exam
{
    public class ExamAssignmentDetailsViewModel
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsSentToStudents { get; set; }
        public bool IsOnline { get; set; }
        public bool IsInLab { get; set; }
        public bool RequireAttendanceBeforeExam { get; set; }
        public int StudentCount { get; set; }
        public bool HasQuestionsSelected { get; set; }


        public List<CurriculumDetailVm> Curriculums { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        // ✅ جديدة
        public int ManualCount { get; set; }
        public int AutoCount { get; set; }
        public ExamAssignmentToBatch Assignment { get; internal set; }
        public List<ExamQuestion> Questions { get; internal set; }
        public List<SectionGroupVm> Sections { get; set; } = new();
        public string? ReferenceCode { get; set; }

        public class CurriculumDetailVm
        {
            public string CurriculumTitle { get; set; }
            public int QuestionCount { get; set; }
        }

        // 🔹 مجموعة المحور
        public class SectionGroupVm
        {
            public int SectionId { get; set; }
            public string SectionTitle { get; set; }
            public List<SectionQuestionVm> Questions { get; set; } = new();
        }



        // 🔹 السؤال داخل المحور
        public class SectionQuestionVm
        {
            public Guid QuestionId { get; set; }
            public string Title { get; set; }
            public string LessonTitle { get; set; }
            public int DifficultyLevel { get; set; }
            public string CorrectAnswer { get; set; }
            public int Order { get; set; }
        }

    }
}
