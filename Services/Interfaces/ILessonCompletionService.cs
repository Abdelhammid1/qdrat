using QdratNew.DTOs;
using QdratNew.ViewModels.Homework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface ILessonCompletionService
    {
        /// <summary>
        /// تسجيل المؤشرات المنتهية
        /// </summary>
        Task<bool> RegisterCompletedLessonsAsync(LessonCompletionInputDto dto);

        /// <summary>
        /// تجهيز بيانات صفحة تأكيد توليد الواجب
        /// </summary>
        Task<ConfirmHomeworkViewModel> PrepareConfirmHomeworkViewModelAsync(int batchId, int? lectureId = null);

        /// <summary>
        /// توليد الواجبات بناءً على المؤشرات
        /// </summary>
        Task<bool> GenerateHomeworksAsync(ConfirmHomeworkViewModel vm, bool forceGenerateAnyway);

        /// <summary>
        /// جلب المحاور المكتملة للدفعة
        /// </summary>
        Task<List<CompletedSectionSummaryViewModel>> GetCompletedSectionsForBatchAsync(int batchId);

        /// <summary>
        /// توليد واجب إضافي لمجموعة من المحاور والطلاب
        /// </summary>
        Task<bool> GenerateHomeworksForExtraSetAsync(int homeworkSetId, List<int> sectionIds, List<int> studentIds, int questionsPerStudent);
    }
}
