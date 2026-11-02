using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S2: المصدر الوحيد لفلترة الملكية
    public interface IPlacementExamScopeGuard
    {
        /// <summary>الاختبارات التي يصل إليها النطاق. Admin: OwnerPartnerId == null بأي نوع (كما كانت Actions الأدمن)، Partner: اختباراته من نوع تحديد المستوى فقط.</summary>
        IQueryable<Exam> ExamsInScope(ApplicationDbContext db, PlacementScope scope);

        /// <summary>اختبارات تحديد المستوى المرئية للنطاق (Admin: OwnerPartnerId == null ، Partner: == PartnerId)</summary>
        IQueryable<Exam> PlacementExams(ApplicationDbContext db, PlacementScope scope);

        /// <summary>Partner: b.Branch.PartnerId == PartnerId</summary>
        IQueryable<Batch> Batches(ApplicationDbContext db, PlacementScope scope);

        /// <summary>Partner: Branch.PartnerId == PartnerId و IsActiveForLearning</summary>
        IQueryable<Student> Students(ApplicationDbContext db, PlacementScope scope);

        /// <summary>PPL-S3: لعرض النتائج. Partner: طلاب فروعه حتى المؤرشفين، Admin: كل الطلاب.</summary>
        IQueryable<Student> StudentsWithResults(ApplicationDbContext db, PlacementScope scope);

        Task<bool> CanAccessExamAsync(ApplicationDbContext db, PlacementScope scope, int examId, CancellationToken ct = default);

        /// <summary>PPL-S3: Admin: مرفوض إن كان الاختبار لشريك فقط (غير الموجود يبقى كما كان). Partner: أي اختبار ليس له.</summary>
        Task<bool> IsExamDeniedAsync(ApplicationDbContext db, PlacementScope scope, int examId, CancellationToken ct = default);

        /// <summary>الأدمن بلا قيد ملكية على الدفعات؛ الشريك دفعات فروعه فقط.</summary>
        Task<bool> CanAccessBatchAsync(ApplicationDbContext db, PlacementScope scope, int batchId, CancellationToken ct = default);

        /// <summary>الأدمن بلا قيد ملكية على الطلاب؛ الشريك طلابه النشطون فقط.</summary>
        Task<bool> CanAccessStudentAsync(ApplicationDbContext db, PlacementScope scope, int studentId, CancellationToken ct = default);

        /// <summary>PPL-S3: الأدمن بلا قيد؛ الشريك طلاب فروعه حتى المؤرشفين.</summary>
        Task<bool> CanAccessStudentResultsAsync(ApplicationDbContext db, PlacementScope scope, int studentId, CancellationToken ct = default);

        /// <summary>التعيين يتبع اختبارًا في النطاق؛ وللشريك يجب أن يكون الطالب من فروعه (حتى لو مؤرشفًا، للاطلاع على نتائجه).</summary>
        Task<bool> CanAccessAssignmentAsync(ApplicationDbContext db, PlacementScope scope, int assignmentId, CancellationToken ct = default);
    }
}
