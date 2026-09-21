namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSkillSessionResultViewModel
    {
        public int Id { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double Score => TotalQuestions == 0 ? 0 : Math.Round((double)CorrectAnswers / TotalQuestions * 100, 2);

        public DateTime CreatedAt { get; set; }

          }
    }

