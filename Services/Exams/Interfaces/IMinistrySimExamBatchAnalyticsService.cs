using System.Threading.Tasks;
using QdratNew.Services.Exams.Models;

namespace QdratNew.Services.Exams.Interfaces
{
    // Sprint 18 (MSE-K / K1): استخراج منطق تحليلات الدفعة (BatchAnalytics — MSE-I، Sprint 15/16) من الـController
    // إلى خدمة، بلا تغيير سلوكها، + إضافة GetBatchAverageStatsAsync الخفيف لإعادة استخدامه في تقرير ولي الأمر
    // (ParentReport) دون تكرار نفس منطق حساب متوسطات الدفعة.
    public interface IMinistrySimExamBatchAnalyticsService
    {
        Task<MinistrySimExamBatchAnalyticsLookup> GetBatchAnalyticsAsync(int ministrySimExamId, int batchId);

        Task<MinistrySimExamBatchAverageStatsVm> GetBatchAverageStatsAsync(int ministrySimExamId, int batchId);
    }
}
