using System;

namespace QdratNew.ViewModels.Exam
{  
    public class ExamReviewOptionVm
    {
       
        public string? Text { get; set; }

        public string? ImageUrl { get; set; }

      
        public bool IsCorrect { get; set; }

        /// <summary>
        /// هل هذا الخيار هو الذي اختاره الطالب؟
        /// (اختياري للتمييز في العرض)
        /// </summary>
        public bool IsSelected { get; set; }
    }
}
