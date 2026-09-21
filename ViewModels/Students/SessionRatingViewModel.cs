using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class SessionRatingViewModel
    {
        public int SessionId { get; set; }
        public int StudentId { get; set; } // ✅ أضف هذا السطر

        [Range(1, 5)]
        public int TrainerClarity { get; set; }

        [Range(1, 5)]
        public int TrainerCommitment { get; set; }

        [Range(1, 5)]
        public int SessionBenefit { get; set; }

        public string? Comment { get; set; }

        // Optional display info
        public string InstructorName { get; set; }
        public string BranchName { get; set; }
        public DateTime SessionDate { get; set; }

    
    }


}
