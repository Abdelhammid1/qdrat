using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IAdaptiveQuestionPickerService
    {
        Task<List<Guid>> PickQuestionsAsync(AdaptiveQuestionPickRequest request);
    }
}
