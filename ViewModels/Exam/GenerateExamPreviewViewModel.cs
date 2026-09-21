namespace QdratNew.ViewModels.Exam
{
    public class GenerateExamPreviewViewModel
    {
        public int BatchId { get; set; }
        public int SectionId { get; set; }
        public int CurriculumId { get; set; }
        public int LessonId { get; set; }

        public string SectionTitle { get; set; }
        public string BatchName { get; set; }
        public string InstructorName { get; set; }

        public int CompletedLessonsCount { get; set; }
        public int TotalLessonsCount { get; set; }
        public int AvailableQuestionsCount { get; set; }

        public bool IsOnline { get; set; } // من الاختيارات في الصفحة
        public DateTime? ScheduledDate { get; set; } // يتم إدخاله




    }

}
