using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Students;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentExamDashboardService
    {
        /// <summary>
        /// 🔹 الحصول على لوحة تحكم الاختبارات للطالب (الملخص + التحليل + المقارنات)
        /// </summary>
        Task<StudentExamDashboardViewModel> GetDashboardAsync(int studentId);

        /// <summary>
        /// 🔹 جلب قائمة الاختبارات المطلوبة فقط (التي لم تُحل بعد)
        /// </summary>
        Task<List<ExamListVm>> GetRequiredExamsAsync(int studentId);

        /// <summary>
        /// 🔹 جلب قائمة الاختبارات المكتملة (التي تم تسليمها)
        /// </summary>
        Task<List<ExamListVm>> GetCompletedExamsAsync(int studentId);

        /// <summary>
        /// 🔹 جلب قائمة الاختبارات المتأخرة (تجاوزت وقتها أو فترة السماح)
        /// </summary>
        Task<List<ExamListVm>> GetLateExamsAsync(int studentId);

        /// <summary>
        /// 🔹 جلب جميع المناهج المرتبطة بالطالب بشكل ديناميكي
        /// </summary>
        Task<List<SelectListItem>> GetStudentCurriculumsAsync(int studentId);

        /// <summary>
        /// 🔹 جلب بيانات Dashboard إحصائية خاصة بمنهج معين لتحديث الرسوم البيانية
        /// </summary>ش
        Task<ExamDashboardChartVm> GetDashboardDataByCurriculumAsync(int studentId, int curriculumId);

        /// <summary>
        /// 🔹 جلب بيانات جميع الاختبارات التفصيلية للطالب
        /// </summary>
        Task<StudentExamFullDataVm> GetExamFullDataAsync(int studentId);
    }
}
