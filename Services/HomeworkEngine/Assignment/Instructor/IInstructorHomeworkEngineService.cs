using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.HomeworkEngine.Instructor
{
    public interface IInstructorHomeworkEngineService
    {
        Task<int> CreateHomeworkForBatchAsync(
            int batchId,
            string title,
            List<Guid> questionIds,
            DateTime? startAt,
            DateTime? endAt,
            string instructorUserId);
    }
}