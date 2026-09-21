using QdratNew.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IEnhancementSkillService
    {
        Task<int> GenerateEnhancementSkillsAsync(int batchId, int lectureId, List<int> lessonIds, int questionsPerStudent = 5);
        Task<List<EnhancementSkillAssignment>> GetAssignmentsForStudentAsync(int studentId, int setId);
        Task<bool> SubmitAnswerAsync(int assignmentId, string studentAnswer);
        Task<EnhancementSkillResult> GetResultForStudentAsync(int studentId, int setId);
    }
}
