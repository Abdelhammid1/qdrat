using QdratNew.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IAttendanceService
    {
        Task<int> GetTotalLecturesForStudentAsync(int studentId);
        Task<int> GetAttendedLecturesCountAsync(int studentId);
        Task<int> GetAbsentLecturesCountAsync(int studentId);
        Task<int> GetExcusedAbsencesCountAsync(int studentId);
        Task<List<AttendanceRecord>> GetAttendanceDetailsAsync(int studentId);
    }
}
