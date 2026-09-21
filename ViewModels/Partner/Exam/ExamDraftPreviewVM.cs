namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamDraftPreviewVM
    {

        public int DraftId { get; set; }
        public string Title { get; set; }

        public int TotalQuestions { get; set; }

        public List<ExamDraftSectionGroupVM> Sections { get; set; }
            = new();

  
   
        public string CourseName { get; set; } = "";

        public string CurriculumName { get; set; } = "";


        public List<ExamDraftLessonGroupVM> LessonGroups { get; set; }
            = new();
    }
}
