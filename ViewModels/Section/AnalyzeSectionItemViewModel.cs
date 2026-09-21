namespace QdratNew.ViewModels.Section
{
    public class AnalyzeSectionItemViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int CompletedLessonsCount { get; set; }
        public string LastLectureTitle { get; set; }
        public DateTime? LastLectureDate { get; set; }
        public bool HasHomework { get; set; }
        public bool HasExam { get; set; }
        public float CompletionPercentage { get; set; }

        // 🟢 الخصائص المطلوبة من الصفحة:
        public bool IsExamGenerated { get; set; }
        public bool IsHomeworkGenerated { get; set; }
        public int TotalQuestions { get; set; }


    }
}
