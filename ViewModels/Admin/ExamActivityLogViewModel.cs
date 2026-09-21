using QdratNew.Entities;

namespace QdratNew.ViewModels.Admin
{
    // Sprint 4 (EC4) — شاشة عرض سجل تعديلات أسئلة الاختبار (AdminToolsController.ExamActivityLog)
    public class ExamActivityLogViewModel
    {
        public int? StudentId { get; set; }
        public int? ExamAssignmentId { get; set; }
        public int? ExamAssignmentToStudentId { get; set; }

        public List<AdminActivityLog> Items { get; set; } = new();
    }
}
