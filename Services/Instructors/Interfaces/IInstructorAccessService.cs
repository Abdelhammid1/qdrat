using QdratNew.Enums;

namespace QdratNew.Services.Instructors.Interfaces
{
    public interface IInstructorAccessService
    {
        Task<bool> IsPartnerInstructorAsync(string userId);

        Task<int?> GetInstructorIdAsync(string userId);

        Task<List<int>> GetAccessibleBatchIdsAsync(string userId);

        Task<List<int>> GetAccessibleCurriculumIdsAsync(string userId);

        Task<bool> HasRoleInBatchAsync(
            string userId,
            int batchId,
            InstructorBatchRoleType roleType);

        Task<bool> CanEditQuestionBankAsync(string userId);
    }

}
