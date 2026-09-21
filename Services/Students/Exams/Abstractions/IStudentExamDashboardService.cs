using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Exam;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Abstractions
{
    public interface IStudentExamDashboardService
    {
        // =========================
        // Dashboard Summary
        // =========================
        Task<StudentExamDashboardViewModel> GetDashboardAsync(int studentId);

        // =========================
        // Curriculum Filters (OLD)
        // =========================
        Task<List<SelectListItem>> GetStudentCurriculumsAsync(int studentId);

        // =========================
        // Charts Data
        // =========================
        Task<object> GetDashboardDataByCurriculumAsync(int studentId, int curriculumId);
    }
}
