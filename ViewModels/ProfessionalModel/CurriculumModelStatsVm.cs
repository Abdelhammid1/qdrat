using ModelEntity = QdratNew.Entities.ProfessionalModel;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class CurriculumModelStatsVm
    {
        public int    CurriculumId    { get; set; }
        public string CurriculumTitle { get; set; }
        public List<ModelEntity> HomeworkModels { get; set; } = new();
        public List<ModelEntity> ExamModels     { get; set; } = new();
        public int HomeworkCount => HomeworkModels.Count;
        public int ExamCount     => ExamModels.Count;
        public int TotalCount    => HomeworkCount + ExamCount;
    }
}
