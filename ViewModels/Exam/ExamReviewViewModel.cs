using QdratNew.Enums;
using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Exam
{
    public class ExamReviewViewModel
    {
        
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public int TotalQuestions { get; set; }
        public List<ExamReviewQuestionVm> Questions { get; set; } = new();



        // ✅ الخصائص الجديدة الخاصة بالوقت
        public string TimeSpentFormatted { get; set; }
        public string TimeExplanation { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        

        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }

        // 🆕 عدد إجابات "لا أعرف الإجابة" (مُتضمَّنة أصلًا داخل WrongAnswers)
        public int DontKnowAnswers { get; set; }

        public double ScorePercentage { get; set; }

        public double TimeSpentMinutes { get; set; } // ✅ جديد
        public int SkippedQuestions { get; set; }
        public double AverageTimePerQuestion { get; internal set; }
        public bool IsIndividual { get; internal set; }

        public List<ExamReviewSectionVm> Sections { get; set; } = new();
        public bool IsRTL { get; internal set; }
    }

    public class ExamReviewQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }

        // 🆕 هل اختار الطالب "لا أعرف الإجابة"
        public bool IsDontKnowAnswer { get; set; }

        public bool IsQuantitative { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public double TimeTakenSeconds { get; set; } = 0; // ⏱ الوقت المستغرق لحل السؤال

        public int QuestionOrder { get; set; }

        public string VerbalPassageContent { get; set; }
        public string ImageUrl { get; set; }

        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }

        public string ComparisonValue1 { get; set; }
        public string ComparisonValue2 { get; set; }

   

        public List<QuestionOptionVm> Options { get; set; } = new();

        // 🆕 نضيف الفهرس لتحديد الخيار الذي اختاره الطالب
        public int? StudentSelectedOptionIndex { get; set; }
        public string VerbalPassageTitle { get; internal set; }
        public bool IsSkipped { get; internal set; }
        public bool IsRTL { get; set; }
        public string? VerbalPassageMediaUrl { get; internal set; }
        public PassageType? VerbalPassageType { get; internal set; }
        public object VerbalPassageDurationSeconds { get; internal set; }
        public object VerbalPassageRequireFullListen { get; internal set; }
        public string VideoUrl { get; internal set; }
        public string Explanation { get; internal set; }
    }
}
