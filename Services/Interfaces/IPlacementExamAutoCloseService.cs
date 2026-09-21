namespace QdratNew.Services.Interfaces
{
    public interface IPlacementExamAutoCloseService
    {
        Task<int> CloseExpiredExamsAsync();
    }
}
