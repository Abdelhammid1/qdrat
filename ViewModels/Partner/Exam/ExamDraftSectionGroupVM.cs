namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamDraftSectionGroupVM
    {

        public int? SectionId { get; set; }
        public string SectionTitle { get; set; }

        public List<ExamDraftQuestionItemVM> Questions { get; set; }
            = new();


    }
}
