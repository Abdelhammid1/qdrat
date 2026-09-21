using QdratNew.Entities;
using System.Security.Claims;

namespace QdratNew.Services.Admin
{
    public interface IEmployeeBatchAccessService
    {
        /// <summary>المالك أو المبرمج — وصول كامل بلا قيود</summary>
        bool IsPrivilegedUser(ClaimsPrincipal user);

        /// <summary>
        /// هل يملك المستخدم صلاحية "Batches" كاملة (غير مخصّصة/Custom) عبر بروفايل الصلاحيات (Matrix)؟
        /// في هذه الحالة يُتجاوز فلتر EmployeeBatchAccess لأن البروفايل يمنحه رؤية كل الدفعات صراحة.
        /// </summary>
        Task<bool> HasFullBatchesAccessAsync(string userId);

        /// <summary>الدفعات المسموح بها لمستخدم عبر EmployeeBatchAccess</summary>
        Task<List<int>> GetPermittedBatchIdsAsync(string userId, InstructorBatchFeature feature);

        /// <summary>هل يملك المستخدم صلاحية ميزة محددة على دفعة بعينها؟</summary>
        Task<bool> HasBatchFeatureAccessAsync(string userId, int batchId, InstructorBatchFeature feature);
    }
}
