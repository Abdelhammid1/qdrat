using System;
using System.Collections.Generic;

namespace QdratNew.Services.PartnerHomework.ProfessionalModels
{
    public interface IProfessionalModelAssignmentService
    {
        void SendModelToStudents(
            int modelId,
            int partnerId,
            int subscriptionPeriodId,
            List<Guid>? overrideQuestionIds = null
        );
    }
}
