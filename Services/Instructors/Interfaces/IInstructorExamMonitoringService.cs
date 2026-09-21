using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Instructor.Exam;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Interfaces
{
    public interface IInstructorExamMonitoringService
    {
        // ============================
        // 👨‍🎓 Students
        // ============================
        Task<PartnerExamStudentsVM> GetExamStudentsAsync(int examAssignmentId, int instructorId);

        // 🔥 جديد (Individual Exams)
        Task<PartnerExamStudentsVM> GetIndividualExamStudentsAsync(int examAssignmentId, int instructorId);

        // ============================
        // 📈 Attempts
        // ============================
        Task<PartnerExamAttemptsVM> GetStudentAttemptsAsync(int examAssignmentId, int studentId, int instructorId);

        // ============================
        // 📊 Report (Dashboard)
        // ============================
        Task<InstructorExamReportVM> GetExamReportAsync(int examAssignmentId, int instructorId);

        Task<HomeworkReviewViewModel> GetExamStudentReviewAsync(int examAssignmentId, int studentId, int instructorId);

        Task<HomeworkAnalyticsViewModel> GetExamStudentReportAsync(int examAssignmentId, int studentId, int instructorId);
    }
}