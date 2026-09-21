namespace QdratNew.ViewModels.Section
{
    public class ManageSectionUnitViewModel
    {
        public int UnitId { get; set; }
        public string UnitTitle { get; set; }
        public List<SectionCheckboxViewModel> Sections { get; set; } = new();
    }

    public class SectionCheckboxViewModel
    {
        public int SectionId { get; set; }
        public string Title { get; set; }
        public bool IsLinked { get; set; }
    }
}
