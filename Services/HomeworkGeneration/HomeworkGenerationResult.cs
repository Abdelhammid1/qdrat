namespace QdratNew.Services.HomeworkGeneration
{
    public class HomeworkGenerationResult
    {
        public bool Success { get; set; }

        public int HomeworkSetId { get; set; }

        public int TotalQuestions { get; set; }

        public List<int> QuestionIds { get; set; } = new();

        public List<string> Warnings { get; set; } = new();

        public string? ErrorMessage { get; set; }


    }
}
