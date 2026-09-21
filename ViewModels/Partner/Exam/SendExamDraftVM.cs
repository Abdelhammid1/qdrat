using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class SendExamDraftVM
    {
        public int DraftId { get; set; }

        public string ExamTitle { get; set; }

        public int CourseId { get; set; }
        public int CurriculumId { get; set; }

        public List<int> BatchIds { get; set; } = new();

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int DurationMinutes { get; set; }

        public List<BatchSelectItemVM> AvailableBatches { get; set; } = new();


    

        public List<SelectListItem> Courses { get; set; } = new();

        // ✅ بدل ViewBag
     
    }
}
