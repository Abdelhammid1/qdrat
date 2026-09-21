using System;
using System.Collections.Generic;
using QdratNew.ViewModels.Partner.HomeworkDraft;

namespace QdratNew.Services.HomeworkEngine.Assignment
{
    public interface IHomeworkEngineAssignmentService
    {
        void SendDraftToBatches(
            SendHomeworkDraftVM model,
            int draftId,
            string assignedByUserId
        );

        int SendDirectToStudents(
            string title,
            int batchId,
            int? lectureId,
            DateTime? startAt,
            DateTime endAt,
            List<Guid> questionIds,
            List<int> studentIds,
            string assignedByUserId
        );
    }
}