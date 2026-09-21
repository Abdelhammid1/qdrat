using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace QdratNew.Services.Interfaces
{
    public interface IUserContextService
    {
        string ActiveRole { get; }
        List<string> AvailableRoles { get; }
        bool HasMultipleRoles { get; }
    }
}
