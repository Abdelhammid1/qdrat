using Microsoft.AspNetCore.Authorization;
using QdratNew.Enums;

namespace QdratNew.Security
{
    public class AdminPermissionAttribute : AuthorizeAttribute
    {
        public AdminPermissionAttribute(
            string controllerName,
            string action)
        {
            Policy = $"{controllerName}:{action}";
        }
    }

}
