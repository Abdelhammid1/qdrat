namespace QdratNew.ViewModels.Partner.Exam
{
    public class PartnerExamStudentsVM
    {
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }

        public List<PartnerExamStudentRowVM> Students { get; set; } = new();
    }

    public class PartnerExamStudentRowVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public string Status { get; set; }
        public bool IsSubmitted { get; set; }
    }

}
