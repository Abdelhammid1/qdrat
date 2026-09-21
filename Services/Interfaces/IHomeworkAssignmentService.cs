using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IHomeworkAssignmentService
    {
        Task AssignMissingHomeworksToStudentAsync(int studentId);
        Task<int> GetTotalHomeworkCountForStudentBatchAsync(int studentId);
        Task<int> GetCompletedHomeworkCountAsync(int studentId);
        Task<int> GetPendingHomeworkCountAsync(int studentId);
    }
}
