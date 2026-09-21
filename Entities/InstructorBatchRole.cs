using QdratNew.Enums;

namespace QdratNew.Entities
{
    // ⚠️ يُكتب حصريًا من Areas/Partner/Controllers/PartnerInstructorsController.cs (سياق الشريك) — ليس
    // جزءًا من مسار ربط الأدمن العادي (InstructorCurriculumBatchesController). يُقرأ ضمن
    // InstructorScopeService كمصدر نطاق إضافي مستقل (Epic B2).
    public class InstructorBatchRole
    {
        public int Id { get; set; }

        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public InstructorBatchRoleType RoleType { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }
}
