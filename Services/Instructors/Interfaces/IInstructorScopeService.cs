using QdratNew.Entities;

namespace QdratNew.Services.Instructors.Interfaces
{
    public interface IInstructorScopeService
    {
        Task<Instructor> GetInstructorByUserIdAsync(string userId);

        Task<List<int>> GetAllowedBatchIdsAsync(int instructorId);

        Task<List<int>> GetAllowedCourseIdsAsync(int instructorId);

        Task<List<int>> GetAllowedCurriculumIdsAsync(int instructorId);

        Task<List<int>> GetAllowedStudentIdsAsync(int instructorId);

        Task<bool> CanAccessBatchAsync(int instructorId, int batchId);

        Task<bool> CanAccessCourseAsync(int instructorId, int courseId);

        Task<bool> CanAccessCurriculumAsync(int instructorId, int curriculumId);

        Task<bool> CanAccessStudentAsync(int instructorId, int studentId);

        /// <summary>
        /// دفعات مُصرَّح بها عبر جدول InstructorBatchPermission لميزة محددة
        /// </summary>
        Task<List<int>> GetPermittedBatchIdsAsync(int instructorId, InstructorBatchFeature feature);

        /// <summary>
        /// الدفعات المرتبطة مباشرة بالمدرب عبر InstructorCurriculumBatches فقط (للداشبورد)
        /// </summary>
        Task<List<int>> GetDirectBatchIdsAsync(int instructorId);

        /// <summary>
        /// المناهج المرتبطة مباشرة بالمدرب عبر InstructorCurriculumBatches فقط (للداشبورد)
        /// </summary>
        Task<List<int>> GetDirectCurriculumIdsAsync(int instructorId);
    }
}
