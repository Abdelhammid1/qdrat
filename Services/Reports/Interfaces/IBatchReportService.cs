using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Interfaces
{
    /// <summary>
    /// تقارير الدفعة (واجبات + اختبارات)
    /// مخصصة لإدارة الشريك
    /// PPL-S1: كل ميثود مقيدة بدفعات الشريك partnerId (null عند عدم الملكية)
    /// </summary>
    public interface IBatchReportService
    {
        /// <summary>
        /// التقرير العام للدفعة (ملخص)
        /// </summary>
        BatchReportVM GetBatchReport(int partnerId, int batchId);

        /// <summary>
        /// تقرير الواجبات داخل الدفعة
        /// </summary>
        BatchHomeworkReportVM GetBatchHomeworkReport(int partnerId, int batchId);

        /// <summary>
        /// تقرير الاختبارات داخل الدفعة
        /// </summary>
        BatchExamReportVM GetBatchExamReport(int partnerId, int batchId);
    }
}
