using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Instructor.Homework
{
    public class CreateInstructorHomeworkVm
    {
        [Required]
        public int BatchId { get; set; }

        [Required]
        public string Title { get; set; }

        public DateTime? StartAt { get; set; }

        public DateTime? EndAt { get; set; }

        public List<Guid> QuestionIds { get; set; } = new();

        public List<int> AvailableBatches { get; set; } = new();
    }
}