namespace QdratNew.Entities
{
    public class Unit
    {
        public int Id { get; set; }
        public string Title { get; set; }  // اسم الوحدة (مثل "مفهوم الأعداد" أو "المعادلات")

        public List<Lesson> Lessons { get; set; } = new List<Lesson>();  // ✅ تحتوي الوحدة على عدة مؤشرات (دروس)

        public ICollection<SectionUnit> SectionUnits { get; set; } = new List<SectionUnit>();  // ✅ العلاقة الجديدة Many-to-Many


    }

}
