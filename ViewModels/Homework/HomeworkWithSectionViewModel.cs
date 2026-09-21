using QdratNew.Entities;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkWithSectionViewModel
    {
        public QdratNew.Entities.Homework Homework { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public string InstructorName { get; set; } = "—";

    }
}
