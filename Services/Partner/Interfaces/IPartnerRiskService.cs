using QdratNew.ViewModels.Partner.Risk;

namespace QdratNew.Services.Partner.Interfaces
{
    public interface IPartnerRiskService
    {
        Task<List<StudentRiskVM>> CalculateRiskAsync(int partnerId);
    }
}