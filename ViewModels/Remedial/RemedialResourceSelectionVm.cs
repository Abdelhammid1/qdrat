using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialResourceSelectionVm
    {
        public int StudentId { get; set; }
        public int SectionId { get; set; }
        public int LessonId { get; set; }

        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        public List<QuestionOptionVm> WrongQuestions { get; set; } = new();
        public List<VideoOptionVm> AvailableVideos { get; set; } = new();

        [Display(Name = "الفيديوهات المختارة")]
        public List<int> SelectedVideoIds { get; set; } = new();

        [Display(Name = "الأسئلة للاختبار المصغر")]
        public List<Guid> SelectedQuestionIds { get; set; } = new();
    }

    public class QuestionOptionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
    }

    public class VideoOptionVm
    {
        public int VideoId { get; set; }
        public string VideoTitle { get; set; }
        public string VideoUrl { get; set; }
    }
}
