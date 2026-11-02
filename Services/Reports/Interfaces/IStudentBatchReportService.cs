using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Interfaces
{
    /// <summary>
    /// تقارير طالب داخل دفعة محددة
    /// PPL-S1: كل ميثود مقيدة بدفعات الشريك partnerId (null عند عدم الملكية)
    /// </summary>
    public interface IStudentBatchReportService
    {
        /// <summary>
        /// التقرير الشامل للطالب داخل الدفعة
        /// </summary>
        StudentInBatchReportVM GetStudentReport(int partnerId, int batchId, int studentId);

        /// <summary>
        /// تفاصيل واجب واحد للطالب
        /// </summary>
        StudentHomeworkDetailVM GetStudentHomeworkDetails(
            int partnerId,
            int homeworkSetId,
            int studentId);

        /// <summary>
        /// تفاصيل اختبار واحد للطالب
        /// </summary>
        StudentExamDetailVM GetStudentExamDetails(
            int partnerId,
            int examAssignmentId,
            int studentId);


        HomeworkStudentsVM GetHomeworkStudents(int partnerId, int homeworkSetId);

        ExamStudentsVM GetExamStudents(int partnerId, int examAssignmentId);




    }
}
