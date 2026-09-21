using QdratNew.Enums;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkReviewViewModel
    {
        public int HomeworkSetId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int TimeSpentMinutes { get; set; }
        public int TimeSpentSeconds { get; set; }
        public string TimeSpentFormatted { get; set; }
        public string? TimeExplanation { get; set; }
   
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public string VerbalPassageTitle { get; set; }

      


        public List<HomeworkQuestionReviewItems> Questions { get; set; } = new();
        public int SkippedAnswers { get; internal set; }
        public bool IsRTL { get; internal set; }
    }


    public class HomeworkQuestionReviewItems
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = "";
        public string? ImageUrl { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsQuantitative { get; set; }

        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public double TimeTakenSeconds { get; set; }
        public string? VerbalPassageTitle { get; set; }

        public string? VerbalPassageContent { get; set; }
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }
        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }
        public string TimeTakenFormatted
        {
            get
            {
                var totalSeconds = (int)TimeTakenSeconds;
                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;
                return $"{minutes} دقيقة {seconds} ثانية";
            }
        }

        public List<HomeworkOptionReviewItem> Options { get; set; } = new();
        public bool IsRTL { get; internal set; }
        public string? VerbalPassageMediaUrl { get; set; }
        public PassageType? VerbalPassageType { get; set; }
        public string? VideoUrl { get; internal set; }
        public string? Explanation { get; internal set; }
    }






}
