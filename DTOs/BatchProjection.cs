using QdratNew.Enums;

namespace QdratNew.DTOs
{
    public sealed class BatchProjection
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public QdratNew.Enums.GenderType Gender { get; set; }   // مهم لاستخدام GetDisplayName()
        public string CourseName { get; set; }
        public string BranchName { get; set; }
        public string BranchState { get; set; }
        public string BranchLocation { get; set; }
    }

}
