using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Abstractions
{
    public interface IExamResultEngine
    {
        Task FinalizeAsync(
            ExamResultContext context,
            int reviewSeconds
        );

        // 🔹 جديد — لدعم الاختبارات القديمة (اختبارات دفعة — ExamAssignmentId)
        Task GenerateSnapshotIfMissingAsync(
            int examAssignmentId,
            int studentId
        );

        // Sprint 3 (ED3) — نفس المنطق لكن للاختبارات الفردية (ExamAssignmentToStudentId)
        Task GenerateSnapshotIfMissingForIndividualAsync(
            int examAssignmentToStudentId,
            int studentId
        );
    }
}
