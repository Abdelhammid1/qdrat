using QdratNew.ViewModels.Reports;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IRemedialReportService
    {
        Task<StudentRemedialReportVm> GenerateStudentReportAsync(int studentId, int sessionId);
    }
}
