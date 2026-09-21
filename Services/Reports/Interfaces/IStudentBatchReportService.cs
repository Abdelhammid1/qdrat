using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Interfaces
{
    /// <summary>
    /// تقارير طالب داخل دفعة محددة
    /// </summary>
    public interface IStudentBatchReportService
    {
        /// <summary>
        /// التقرير الشامل للطالب داخل الدفعة
        /// </summary>
        StudentInBatchReportVM GetStudentReport(int batchId, int studentId);

        /// <summary>
        /// تفاصيل واجب واحد للطالب
        /// </summary>
        StudentHomeworkDetailVM GetStudentHomeworkDetails(
            int homeworkSetId,
            int studentId);

        /// <summary>
        /// تفاصيل اختبار واحد للطالب
        /// </summary>
        StudentExamDetailVM GetStudentExamDetails(
            int examAssignmentId,
            int studentId);


        HomeworkStudentsVM GetHomeworkStudents(int homeworkSetId);

        ExamStudentsVM GetExamStudents(int examAssignmentId);




    }
}
