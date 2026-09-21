using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Instructor;

namespace QdratNew.ViewModels.Remedial
{
    public class StudentRemedialAnalysisViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string InstructorName { get; set; }
        public string SectionTitle { get; set; }
        public List<string> FailedIndicators { get; set; }
        public List<IndicatorAnalysisItem> Indicators { get; set; }
        public List<WeakSectionViewModel> WeakSections { get; set; } = new();
    }

    public class IndicatorAnalysisItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
        public int HomeworkQuestionCount { get; set; }
        public int ExamQuestionCount { get; set; }
        public List<QuestionReviewItem> WrongQuestions { get; set; }
    }



}
