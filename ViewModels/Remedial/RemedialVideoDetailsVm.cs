using System.Collections.Generic;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialVideoDetailsVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string VimeoUrl { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string? LessonTitle { get; set; }
        public string? SectionTitle { get; set; }
        public string? CurriculumTitle { get; set; }

        public List<RemedialVideoQuestionVm> Questions { get; set; } = new();
    }

    public class RemedialVideoQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
    }
}
