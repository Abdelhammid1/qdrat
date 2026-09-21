using QdratNew.Enums;


namespace QdratNew.ViewModels.Partner.Exam
{
    public class GenerateExamRequestVM
    {

        // معلومات عامة
        public string ExamTitle { get; set; }

        public int CourseId { get; set; }

        // مجموعة المناهج داخل الدورة
        public List<CurriculumGenerateItemVM> Curriculums { get; set; }
            = new List<CurriculumGenerateItemVM>();


    }
}

