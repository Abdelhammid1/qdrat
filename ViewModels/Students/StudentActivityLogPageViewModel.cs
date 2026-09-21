using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class StudentActivityLogPageViewModel
    {
        public List<StudentActivityLogViewModel> Activities { get; set; }

        // ملخص الأداء
        public int TotalActivities { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public StudentActivityType? CurrentFilter { get; set; }

        // توصيات من الذكاء الاصطناعي
        public List<string>? AIRecommendations { get; set; }


 


    }
}
