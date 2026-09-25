using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QdratNew.ViewModels.Frontend.Register;

namespace QdratNew.Services.Frontend.PublicRegistration;

public interface IPublicRegistrationService
{
    Task<List<RegisterProgramCardVM>> GetCatalogAsync(CancellationToken ct = default);
    Task<SubmitLeadResult> SubmitLeadAsync(RegisterLeadFormVM form, string? ip, CancellationToken ct = default);
    void InvalidateCatalog();
}

public record SubmitLeadResult(bool Success, int? LeadId, Dictionary<string, string> Errors);
