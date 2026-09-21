using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class CurriculumModule
    {
        public int Id { get; set; }
        public string Title { get; set; }  // اسم الوحدة التعليمية
        public string Content { get; set; }  // محتوى الوحدة
        public int CurriculumId { get; set; }  // ارتباط الوحدة بالمنهج
        public Curriculum Curriculum { get; set; }
    }

}
