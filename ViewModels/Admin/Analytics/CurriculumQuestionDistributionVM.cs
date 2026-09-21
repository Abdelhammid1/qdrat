namespace QdratNew.ViewModels.Admin.Analytics
{
    public class CurriculumQuestionDistributionVM
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = "";
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
        public int VeryHardCount { get; set; }
        public int Total { get; set; }
    }
}
