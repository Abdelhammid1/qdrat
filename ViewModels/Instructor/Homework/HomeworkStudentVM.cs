namespace QdratNew.ViewModels.Instructor.Homework
{
    public class HomeworkStudentVM
    {
        public int StudentId { get; set; }

        public string StudentName { get; set; }

        public bool IsSolved { get; set; }

        public decimal? Score { get; set; }

        public DateTime? SubmittedAt { get; set; }
    }
}
