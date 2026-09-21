namespace QdratNew.ViewModels.Exam
{
    public class StudentExamAssignmentVm
    {
        public int StudentId { get; set; }

        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        public int? ProfessionalModelId { get; set; } // إذا اختار نموذج

        public int EasyQuestionCount { get; set; } = 3;
        public int MediumQuestionCount { get; set; } = 4;
        public int HardQuestionCount { get; set; } = 3;

        public int DurationMinutes { get; set; } = 30;

        public DateTime ScheduledDate { get; set; } = DateTime.Now.AddHours(1);
        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(2);

        public bool UseProfessionalModel { get; set; }

        public string? ReferenceCode { get; set; } // يمكن إدخاله أو توليده تلقائيًا
    }

}
