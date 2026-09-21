namespace QdratNew.ViewModels.Instructor
{
    public class InstructorExamMissingStudentViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public string ExamTitle { get; set; }
        public DateTime AssignedAt { get; set; }

        // عدد الاختبارات غير المؤداة
        public int MissingExamCount { get; set; }

        // أقدم تاريخ استلام اختبار
        public DateTime EarliestAssignedAt { get; set; }
    }
}
