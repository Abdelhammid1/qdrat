using QdratNew.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace QdratNew.Services.Interfaces
{
    public interface ISectionExamGeneratorService
    {
        Task CheckAndGenerateExamAsync(int batchId, int sectionId);
     
    }

}
