using QdratNew.ViewModels.Partner.Homework;
using System.Collections.Generic;

namespace QdratNew.Services.HomeworkTracking.Interfaces
{
    public interface ISentHomeworkService
    {
        List<SentHomeworkListVM> GetSentHomeworksForPartner(int partnerId);
        void ResetHomeworkForStudent(int homeworkSetId, int studentId);

        List<SentHomeworkStudentVM> GetHomeworkStudents(int homeworkSetId);
    }
}
