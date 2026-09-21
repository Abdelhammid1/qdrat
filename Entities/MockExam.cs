using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class MockExam
    {
        public int Id { get; set; }

        public string Title { get; set; }  // مثل "اختبار محاكي شهر مايو"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        public List<MockExamQuestion> Questions { get; set; } = new();
    }

}
