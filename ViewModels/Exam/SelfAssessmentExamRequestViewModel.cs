using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class SelfAssessmentExamRequestViewModel
    {
        [Required]
        public int CurriculumId { get; set; }

        [Required]
        public int BatchId { get; set; }

        public List<SectionCheckboxItem> Sections { get; set; } = new();

        [Display(Name = "عنوان الاختبار")]
        public string Title { get; set; } = "اختبار تقييمي ذاتي";

        [Display(Name = "عدد الأسئلة")]
        public int QuestionCount { get; set; } = 10;

        [Display(Name = "مدة الاختبار بالدقائق")]
        public int DurationMinutes { get; set; } = 30;


        public List<int> SelectedSectionIds { get; set; } = new();
        public int StudentId { get; set; }

        [Display(Name = "المحاور المكتملة")]
        public List<SectionCheckboxItem> AvailableSections { get; set; } = new();

    }
}
