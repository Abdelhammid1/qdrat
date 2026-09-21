using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Interfaces
{
    // Sprint 2 (Epic B) — يمنع تعديل أسئلة اختبار له محاولات فعلية دون تنظيف مترابط.
    // يستخرج نفس منطق التنظيف الصحيح الموجود بالفعل في
    // ExamIndividualAssignmentsController.Delete بدل تكراره في كل أكشن على حدة.
    public interface IExamAssignmentIntegrityService
    {
        // هل للتكليف محاولات فعلية أو حالة تسليم مسجّلة؟ (فردي أو دفعة — مرّر واحدًا فقط من المعرفين)
        Task<AssignmentAttemptSummary> GetAttemptSummaryAsync(
            int? examAssignmentId, int? examAssignmentToStudentId);

        // تنظيف مترابط لكامل التكليف: يحذف QuestionAttemptNew + يصفّر ExamStudentStatuses
        // (Note/IsSubmitted/Status/SubmittedAt) لنفس التكليف — بدون حذف التكليف نفسه ولا الأسئلة
        // (الأسئلة تُدار من المستدعي).
        Task ResetStudentAttemptsAsync(
            int? examAssignmentId, int? examAssignmentToStudentId);

        // تنظيف أضيق لاستبدال سؤال واحد: يحذف فقط QuestionAttemptNew الخاصة بهذا السؤال لنفس
        // التكليف، ويصفّر Note فقط إن كان التكليف مُسلَّمًا (لإجبار إعادة الحساب دون إفقاد
        // IsSubmitted/AttemptCount الحاليين لبقية الأسئلة).
        Task ResetSingleQuestionAttemptAsync(
            System.Guid questionId, int? examAssignmentId, int? examAssignmentToStudentId);
    }

    public class AssignmentAttemptSummary
    {
        public int AttemptedQuestionsCount { get; set; }
        public bool IsSubmitted { get; set; }
        public int AffectedStudentsCount { get; set; } // >1 فقط في حالة الدفعات
    }
}
