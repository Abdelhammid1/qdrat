namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSkillBatchViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string InstructorName { get; set; }
        public int TotalQuestions { get; set; }
        public List<string> Lessons { get; set; } = new();




    }

}
