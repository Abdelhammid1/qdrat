using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentSafeInsightService
    {
        Task<ParentStudentInsightViewModel> GetSafeInsightAsync(int parentId, int studentId);
        Task<ParentLearningStatusViewModel> GetLearningStatusAsync(int parentId, int studentId);
        Task<ParentHomeworkFollowUpViewModel> GetHomeworkFollowUpAsync(int studentId);
        Task<ParentExamFollowUpViewModel> GetExamFollowUpAsync(int studentId);
        Task<ParentWeeklyReportViewModel> GetWeeklyReportAsync(int studentId);
        Task<ParentMonthlyReportViewModel> GetMonthlyReportAsync(int studentId);
    }
}
