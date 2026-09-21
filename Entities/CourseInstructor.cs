using QdratNew.Entities; // ✅ استيراد Course

namespace QdratNew.Entities
{
    // ⚠️ هذا الكيان مهجور الكتابة من واجهة الأدمن الحالية (لا Controller يكتب فيه) — يُقرأ فقط ضمن
    // InstructorScopeService لأسباب توافق تاريخية. راجع PROMPT/Diagnose_CourseInstructor_Dependency.sql
    // قبل أي تعديل (نتيجة التشغيل الفعلي: صفر مدربين نشطين يعتمدون عليه وحده — Epic B1/B2).
    public class CourseInstructor
    {
        public int CourseID { get; set; }  // 🔹 مفتاح أجنبي من `Course`
        public int InstructorID { get; set; }  // 🔹 مفتاح أجنبي من `Instructor`

        public required Course Course { get; set; }  // ✅ علاقة `Many-to-Many`
        public required Instructor Instructor { get; set; }
    }
}
