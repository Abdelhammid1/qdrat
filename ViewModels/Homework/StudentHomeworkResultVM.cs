using QdratNew.Entities;

namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkResultVM
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public string HomeworkTitle { get; set; }

        public int TotalQuestions { get; set; }

        public int Correct { get; set; }

        public int Wrong { get; set; }

        public double Score { get; set; }

        public List<QdratNew.Entities.Homework> Questions { get; set; } = new();
    }
}