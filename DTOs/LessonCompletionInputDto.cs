namespace QdratNew.DTOs
{
    public class LessonCompletionInputDto
    {
        public int BatchId { get; set; }
        public int SectionId { get; set; }
        public int LectureId { get; set; }
        public List<int> LessonIds { get; set; } = new List<int>();
        public string CompletionTitle { get; set; }
        public string AddedByUserId { get; set; } // يمكن أن يكون الإدمن أو المدرب
        public bool ForceGenerateAnyway { get; set; } = false; // لتجاوز شرط 20 سؤال إن لزم
    }
}
