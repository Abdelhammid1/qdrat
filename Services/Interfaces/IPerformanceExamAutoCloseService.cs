using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceExamAutoCloseService
    {
        /// <summary>
        /// يقوم النظام بفحص إن كان هذا الاختبار للطالب قد انتهت مدته
        /// وإذا انتهى يقوم بتسجيل الإنهاء تلقائيًا دون أي تدخل من الطالب.
        /// </summary>
        /// <param name="studentId">رقم الطالب</param>
        /// <param name="examId">رقم اختبار مؤشر الأداء</param>
        /// <returns>
        /// True إذا تم إغلاق الاختبار تلقائيًا،
        /// False إذا لم تنتهِ المدة أو كان الاختبار مغلقًا مسبقًا.
        /// </returns>
        Task<bool> AutoCloseIfExpiredAsync(int studentId, int examId);
    }
}
