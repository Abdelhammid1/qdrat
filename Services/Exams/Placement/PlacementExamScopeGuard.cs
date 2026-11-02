using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S2
    public sealed class PlacementExamScopeGuard : IPlacementExamScopeGuard
    {
        public IQueryable<Exam> ExamsInScope(ApplicationDbContext db, PlacementScope scope)
        {
            if (!scope.IsPartner)
                return db.Exams.Where(e => e.OwnerPartnerId == null);

            var partnerId = scope.PartnerId!.Value;
            return db.Exams.Where(e => e.OwnerPartnerId == partnerId && e.Type == ExamType.LevelAssessment);
        }

        public IQueryable<Exam> PlacementExams(ApplicationDbContext db, PlacementScope scope)
            => ExamsInScope(db, scope).Where(e => e.Type == ExamType.LevelAssessment);

        public IQueryable<Batch> Batches(ApplicationDbContext db, PlacementScope scope)
        {
            if (!scope.IsPartner)
                return db.Batches;

            var partnerId = scope.PartnerId!.Value;
            return db.Batches.Where(b => b.Branch.PartnerId == partnerId);
        }

        public IQueryable<Student> Students(ApplicationDbContext db, PlacementScope scope)
        {
            if (!scope.IsPartner)
                return db.Students;

            var partnerId = scope.PartnerId!.Value;
            return db.Students.Where(s => s.Branch.PartnerId == partnerId && s.IsActiveForLearning);
        }

        public IQueryable<Student> StudentsWithResults(ApplicationDbContext db, PlacementScope scope)
        {
            if (!scope.IsPartner)
                return db.Students;

            var partnerId = scope.PartnerId!.Value;
            return db.Students.Where(s => s.Branch.PartnerId == partnerId);
        }

        public async Task<bool> IsExamDeniedAsync(ApplicationDbContext db, PlacementScope scope, int examId, CancellationToken ct = default)
            => scope.IsPartner
                ? !await CanAccessExamAsync(db, scope, examId, ct)
                : await db.Exams.AnyAsync(e => e.Id == examId && e.OwnerPartnerId != null, ct);

        public Task<bool> CanAccessStudentResultsAsync(ApplicationDbContext db, PlacementScope scope, int studentId, CancellationToken ct = default)
            => scope.IsPartner
                ? StudentsWithResults(db, scope).AnyAsync(s => s.StudentID == studentId, ct)
                : Task.FromResult(true);

        public Task<bool> CanAccessExamAsync(ApplicationDbContext db, PlacementScope scope, int examId, CancellationToken ct = default)
            => ExamsInScope(db, scope).AnyAsync(e => e.Id == examId, ct);

        public Task<bool> CanAccessBatchAsync(ApplicationDbContext db, PlacementScope scope, int batchId, CancellationToken ct = default)
            => scope.IsPartner
                ? Batches(db, scope).AnyAsync(b => b.Id == batchId, ct)
                : Task.FromResult(true);

        public Task<bool> CanAccessStudentAsync(ApplicationDbContext db, PlacementScope scope, int studentId, CancellationToken ct = default)
            => scope.IsPartner
                ? Students(db, scope).AnyAsync(s => s.StudentID == studentId, ct)
                : Task.FromResult(true);

        public Task<bool> CanAccessAssignmentAsync(ApplicationDbContext db, PlacementScope scope, int assignmentId, CancellationToken ct = default)
        {
            var exams = ExamsInScope(db, scope);
            var assignments = db.ExamAssignments
                .Where(a => a.Id == assignmentId && exams.Any(e => e.Id == a.ExamId));

            if (!scope.IsPartner)
                return assignments.AnyAsync(ct);

            var partnerId = scope.PartnerId!.Value;
            return assignments.AnyAsync(
                a => db.Students.Any(s => s.StudentID == a.StudentId && s.Branch.PartnerId == partnerId), ct);
        }
    }
}
