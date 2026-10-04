using System.Linq.Expressions;
using QdratNew.Entities;

namespace QdratNew.Services.Instructors
{
    /// <summary>
    /// مصدر واحد لشرط «الربط المباشر الفعّال» بين المدرب والمنهج/الدفعة:
    /// دفعة نشطة، غير محذوفة، غير مؤرشفة، ولم ينتهِ تاريخها.
    /// </summary>
    public static class InstructorBatchScope
    {
        public static Expression<Func<InstructorCurriculumBatch, bool>> IsDirectActive(DateTime today)
            => x => x.Batch != null
                    && x.Batch.IsActive
                    && !x.Batch.IsDeleted
                    && !x.Batch.IsArchived
                    && (!x.Batch.EndDate.HasValue || x.Batch.EndDate.Value >= today);
    }
}
