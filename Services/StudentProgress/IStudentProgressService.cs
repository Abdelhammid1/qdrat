public interface IStudentProgressService
{
    Task RecordPerformanceIndicatorProgress(int studentId, int examId);
    Task RecordExamProgress(int studentId, int examAssignmentId);
    Task RecordHomeworkProgress(int studentId, int homeworkSetId);
}
