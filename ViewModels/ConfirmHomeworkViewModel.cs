using Microsoft.AspNetCore.Mvc;
using QdratNew.ViewModels.Homework;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class ConfirmHomeworkViewModel
    {

        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public int CurriculumId { get; set; }
        public string CompletionTitle { get; set; }

        public bool ForceGenerateAnyway { get; set; } = false;

        // ✅ تاريخ بداية الواجب (متى يبدأ الطالب في الحل)
        public DateTime? StartAt { get; set; }

        // ✅ تاريخ نهاية الواجب (آخر موعد للحل)
        public DateTime? EndAt { get; set; }

        public List<LessonSummaryViewModel> Lessons { get; set; } = new List<LessonSummaryViewModel>();






        public int TotalQuestionsToGenerate { get; set; } = 0;

        public string SectionTitle { get; set; }





    }

    public class LessonSummaryViewModel
    {

        public int LessonId { get; set; }

        [Display(Name = "المؤشر")]
        public string LessonTitle { get; set; }

        public int TotalQuestions { get; set; }

        public int ReviewedQuestions { get; set; }

        [Display(Name = "عدد الأسئلة للإرسال")]
        public int QuestionsToUse { get; set; }

        public int SectionId { get; set; }

        public bool? IsManualSelection { get; set; } = false;

        public int ManuallySelectedQuestionsCount { get; set; } = 0;

        // ✅ أضف هذا السطر — هو سبب الخطأ كله
        public List<QuestionSummaryViewModel> SelectedQuestions { get; set; } = new();





        public int CurriculumId { get; set; }

    
        public int TotalQuestionsAvailable { get; set; }
        public int SelectedQuestionCount { get; set; }


    }
}
