using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Batch
{



    public class BatchLessonCompletionViewModel
    {
        public int BatchId { get; set; }

        public List<LessonViewItem> Lessons { get; set; } = new();
    }

    public class LessonViewItem
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public bool IsCompleted { get; set; }  // يتم تحديده في الواجهة
    }
}