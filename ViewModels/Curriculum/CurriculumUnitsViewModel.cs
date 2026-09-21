namespace QdratNew.ViewModels.Curriculum
{
    public class CurriculumUnitsViewModel
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }

        public List<string> UnitTitles { get; set; } = new List<string>();
    }
}
