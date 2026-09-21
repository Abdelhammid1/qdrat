using Microsoft.AspNetCore.Authorization;

namespace QdratNew.Security.AdminPermissions
{
    public sealed class AdminPermissionAuthorizationRequirement : IAuthorizationRequirement
    {
        public string ControllerName { get; }
        public string ActionName { get; }

        // ✅ الكونستركتور الوحيد المعتمد
        public AdminPermissionAuthorizationRequirement(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
                throw new ArgumentException("Policy cannot be null or empty.", nameof(policy));

            // policy format: Controller:Action
            var parts = policy.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                throw new InvalidOperationException(
                    $"Invalid admin permission policy format: '{policy}'. Expected 'Controller:Action'.");

            ControllerName = parts[0];
            ActionName = parts[1];
        }
    }
}
