using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class SendExamDraftVM
    {

        public int DraftId { get; set; }

        [ValidateNever]
        public List<int> BatchIds { get; set; } = new();

        [ValidateNever]
        public List<SelectItemVM> Batches { get; set; } = new();

        public List<int> StudentIds { get; set; } = new();

        [ValidateNever]
        public List<SelectItemVM> Students { get; set; } = new();

        // =========================
        // Exam Data
        // =========================
        [Required]
        public string ExamTitle { get; set; }

        [Required]
        public DateTime StartAt { get; set; }

        [Required]
        public DateTime EndAt { get; set; }

        [Range(1, 300)]
        public int DurationMinutes { get; set; }

        public string ExamMode { get; set; } = "online";


    }
}