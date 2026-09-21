using QdratNew.ViewModels.Exam;
using System;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Generators
{
    public interface IPlacementExamGeneratorService
    {
        /// <summary>
        /// ✅ توليد اختبار تحديد المستوى (Level Assessment)
        /// </summary>
        Task<int> GeneratePlacementTestReturnExamIdAsync(
            int studentId,
            int courseId,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// ✅ توليد اختبار تحديد المستوى وإرجاع رقم التعيين AssignmentId
        /// </summary>
        Task<int> GeneratePlacementTestReturnAssignmentIdAsync(
            int studentId,
            int courseId,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null);



        /// <summary>
        /// ✅ توليد اختبار تحديد المستوى باستخدام توزيع الأسئلة حسب المحاور
        /// </summary>
        Task<int> GeneratePlacementTestFromSectionsAsync(
            int studentId,
            int courseId,
            List<PlacementExamSectionSelectionVm> sections,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null);

    }
}
