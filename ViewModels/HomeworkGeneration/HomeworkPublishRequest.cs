using System;

namespace QdratNew.ViewModels.HomeworkGeneration
{
    public class HomeworkPublishRequest
    {
        public DateTime PublishDate { get; set; }
        public TimeSpan? PublishTime { get; set; }

        public bool IsVisibleImmediately { get; set; }

        public string Notes { get; set; } = string.Empty;
    }
}
