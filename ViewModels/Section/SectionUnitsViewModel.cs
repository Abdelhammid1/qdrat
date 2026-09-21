namespace QdratNew.ViewModels.Section
{
    public class SectionUnitsViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<string> UnitTitles { get; set; } = new List<string>();
    }
}
