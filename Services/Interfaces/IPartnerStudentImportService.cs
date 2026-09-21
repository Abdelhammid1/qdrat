using QdratNew.ViewModels.Partner;

namespace QdratNew.Services.Interfaces
{
    public interface IPartnerStudentImportService
    {
        Task<PartnerStudentImportResult> ImportFromExcelAsync(
       IFormFile file,
       int partnerId,
       int subscriptionPeriodId,
       int courseId,
       int batchId
   );

    }

}
