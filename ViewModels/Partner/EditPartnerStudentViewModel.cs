namespace QdratNew.ViewModels.Partner
{
    public class EditPartnerStudentViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public bool IsActiveForLearning { get; set; }



        public string Gender { get; set; }
        public string Level { get; set; }
        public string School { get; set; }



        public string? CurrentImagePath { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}
