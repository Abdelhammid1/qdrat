using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Interfaces
{
    /// <summary>
    /// تقارير الدفعة (واجبات + اختبارات)
    /// مخصصة لإدارة الشريك
    /// </summary>
    public interface IBatchReportService
    {
        /// <summary>
        /// التقرير العام للدفعة (ملخص)
        /// </summary>
        BatchReportVM GetBatchReport(int batchId);

        /// <summary>
        /// تقرير الواجبات داخل الدفعة
        /// </summary>
        BatchHomeworkReportVM GetBatchHomeworkReport(int batchId);

        /// <summary>
        /// تقرير الاختبارات داخل الدفعة
        /// </summary>
        BatchExamReportVM GetBatchExamReport(int batchId);
    }
}
