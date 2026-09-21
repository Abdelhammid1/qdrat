using QdratNew.Entities;

namespace QdratNew.ViewModels.Students
{
    public class PracticeQuestionVm
    {
        public QdratNew.Entities.Question Question { get; set; }  // ✅ المسار كامل
        public bool IsRepeatedMistake { get; set; }
        public string StudentAnswer { get; internal set; }
        public string CorrectAnswer { get; internal set; }
        public string ExamTitle { get; internal set; }
        public bool IsSkipped { get; internal set; }
    }
}
