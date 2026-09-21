using QdratNew.Entities;

namespace QdratNew.ViewModels.Parent
{
    public class ParentDetailsViewModel
    {
        public int ParentID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string NationalID { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? WhatsAppNumber { get; set; }
        public ParentRelation RelationToStudent { get; set; }
        public DateTime DateCreated { get; set; }
        public bool HasUserAccount { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }

        public List<ParentStudentSummary> Students { get; set; } = new();
    }

    public class ParentStudentSummary
    {
        public int StudentID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string NationalID { get; set; } = string.Empty;
        public string? Level { get; set; }
        public string? School { get; set; }
        public string EnrollmentStatus { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? ProfileImagePath { get; set; }
        public int HomeworkCount { get; set; }
        public double? AvgHomeworkScore { get; set; }
        public double? LastPlacementScore { get; set; }
        public double? LastKpiScore { get; set; }
    }
}
