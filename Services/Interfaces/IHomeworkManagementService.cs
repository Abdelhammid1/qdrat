using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Interfaces
{
    public interface IHomeworkManagementService
    {
        Task<List<HomeworkOverviewViewModel>> GetHomeworksAsync(DateTime? fromDate, DateTime? toDate, int? batchId, bool showArchived = false);
        Task<HomeworkDetailsViewModel> GetHomeworkDetailsAsync(int homeworkSetId);
        Task<bool> ResendHomeworkToBatchAsync(int homeworkSetId, int adminUserId);
        Task<bool> ResendHomeworkToStudentAsync(int homeworkId, int adminUserId);
        Task<int?> GetHomeworkSetIdFromHomework(int homeworkId);

    }
}
