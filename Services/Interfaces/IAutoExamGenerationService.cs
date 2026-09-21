using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IAutoExamGenerationService
    {
        /// <summary>
        /// يحاول توليد اختبار لمحور معين داخل دفعة بناءً على المؤشرات الفعالة المكتملة
        /// </summary>
        /// <param name="batchId">رقم الدفعة</param>
        /// <param name="sectionId">رقم المحور</param>
        /// <returns>رقم الاختبار الذي تم توليده، أو null إذا لم يتم</returns>
        Task<int?> TryGenerateExamIfSectionCompletedAsync(int batchId, int sectionId);

    }
}
