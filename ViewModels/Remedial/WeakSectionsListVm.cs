namespace QdratNew.ViewModels.Remedial
{
    public class WeakSectionsListVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public List<WeakSectionVm> WeakSections { get; set; } = new();
        public string? CurriculumTitle { get; internal set; }
    }
}
