namespace QdratNew.ViewModels.Admin
{
    public class PartnersListViewModel
    {
        public List<PartnerListItemDto> Partners { get; set; } = new();
    }

    public class PartnerListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string? LogoPath { get; set; }
        public bool IsActive { get; set; }
        public int StudentsCount { get; set; }
        public int InstructorsCount { get; set; }
        public int AdminsCount { get; set; }
    }
}
