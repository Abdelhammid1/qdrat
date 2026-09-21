using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.Analytics
{
    public class QuestionDifficultyRecalibrationInputVM
    {
        /// <summary>0 – VeryHardMaxPercent  → صعب جداً</summary>
        public double VeryHardMaxPercent { get; set; } = 15;

        /// <summary>VeryHardMaxPercent+1 – HardMaxPercent  → صعب</summary>
        public double HardMaxPercent { get; set; } = 49;

        /// <summary>HardMaxPercent+1 – MediumMaxPercent  → متوسط</summary>
        public double MediumMaxPercent { get; set; } = 79;

        // سهل = أعلى من MediumMaxPercent

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int? CurriculumId { get; set; }
    }

    public class QuestionDifficultyRecalibrationResultVM
    {
        public int TotalQuestions { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedNoData { get; set; }

        public int VeryHardCount { get; set; }
        public int HardCount { get; set; }
        public int MediumCount { get; set; }
        public int EasyCount { get; set; }

        public double VeryHardMaxPercent { get; set; }
        public double HardMaxPercent { get; set; }
        public double MediumMaxPercent { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public bool IsPreview { get; set; }

        public List<QuestionDifficultyChangeItemVM> ChangedItems { get; set; } = new();
    }

    public class QuestionDifficultyChangeItemVM
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; } = string.Empty;
        public DifficultyLevel OldDifficulty { get; set; }
        public DifficultyLevel NewDifficulty { get; set; }
        public double SuccessRate { get; set; }
        public int UniqueStudents { get; set; }
    }
}
