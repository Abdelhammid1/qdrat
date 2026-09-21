using QdratNew.Entities;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class Section
    {
        public int Id { get; set; }
        public string Title { get; set; }  // اسم المحور (مثل "محور الحساب" أو "محور الجبر")

        public int CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }  // ✅ المحور مرتبط بمنهج معين

        public ICollection<SectionUnit> SectionUnits { get; set; } = new List<SectionUnit>();
        public float? AIScore { get; set; } // نسبة التغطية والتكامل
        public string? AINotes { get; set; } // ملاحظات AI المقترحة
        public ICollection<StudentPerformance> StudentPerformances { get; set; }

        public ICollection<Session> Sessions { get; set; } = new List<Session>(); // ✅



    }

}
