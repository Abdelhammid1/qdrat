namespace QdratNew.ViewModels.Instructor
{
    public class InstructorPerformanceAIViewModel
    {
        public int InstructorId { get; set; }

        public string InstructorName { get; set; }
        public int TotalCurriculums { get; set; }
        public double AverageStudentScore { get; set; }
        public double SuccessRate { get; set; }
        public int AssignedCurriculums { get; set; }
        public int TotalStudents { get; set; }
        public string Recommendation { get; set; }

        // 🧠 خاصية التوصية الذكية (نضيفها الآن)
        public List<string> AIRecommendations { get; set; } = new();




    }

}
