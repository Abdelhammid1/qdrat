using System.Threading.Tasks;
using QdratNew.Services.Instructors.Models;

namespace QdratNew.Services.Instructors.Interfaces
{
    public interface ILectureInstructorSyncService
    {
        // يحدّث InstructorId لكل المحاضرات ذات المصدر Auto ضمن منهج+دفعة معيّنَين. يُرجع عدد المحاضرات التي تأثرت.
        Task<int> SyncAutoLecturesAsync(int curriculumId, int batchId, int newInstructorId);

        // حالة تزامن المحاضرات الحالية لربط مدرب-منهج-دفعة — لعرضها في شاشة الأدمن.
        Task<LectureSyncStatus> GetSyncStatusAsync(int curriculumId, int batchId, int instructorId);
    }
}
