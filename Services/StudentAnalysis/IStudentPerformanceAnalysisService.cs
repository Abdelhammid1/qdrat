using QdratNew.Services.AI;
using QdratNew.ViewModels.StudentAnalysis;

namespace QdratNew.Services.StudentAnalysis
{
    public interface IStudentPerformanceAnalysisService
    {
        Task<StudentPerformanceReportVM> BuildStudentReportAsync(int studentId);
    }
}
