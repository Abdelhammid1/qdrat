using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Enums.Abstractions;
using QdratNew.Services.Exams.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Engines
{
    public class ExamResultEngine : IExamResultEngine
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ExamResultEngine> _logger;

        public ExamResultEngine(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<ExamResultEngine> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task FinalizeAsync(
        ExamResultContext context,
        int reviewSeconds)
        {
            if (context.Kind != ExamKind.General)
                throw new NotSupportedException("ExamResultEngine: هذا التنفيذ يدعم الاختبارات العامة فقط حاليًا.");

            using var db = _contextFactory.CreateDbContext();

            // ======================================================
            // 0) تحديد نوع الاختبار
            // ======================================================
            bool isIndividual =
                context.ExamAssignmentId == null &&
                context.ExamAssignmentToStudentId.HasValue;

            int effectiveAssignmentId = isIndividual
                ? context.ExamAssignmentToStudentId!.Value
                : context.ExamAssignmentId!.Value;

            // ======================================================
            // 1) تحميل الحالة
            // ======================================================
            var status = await db.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == context.StudentId &&
                    (
                        (!isIndividual && s.ExamAssignmentId == effectiveAssignmentId) ||
                        (isIndividual && s.ExamAssignmentToStudentId == effectiveAssignmentId)
                    ));

            if (status == null)
                throw new Exception("ExamStudentStatus غير موجود للاختبار.");

            // ⚠️ لا نخرج مبكرًا لو الـ Snapshot (Note) مفقود رغم التسليم — هذه بالضبط
            // حالة السجلات المتضاربة اللي بيصفّرها Fix_ExamReport_ScoreIntegrity.sql (ED2)
            // ويُعاد حسابها عبر GenerateSnapshotIfMissingAsync (ED3). في التدفق الطبيعي
            // Note يُكتب دائمًا بالتزامن مع IsSubmitted/Status (راجع قسم 6 أدناه)، فهذا
            // الشرط الإضافي لا يغيّر أي سلوك للاختبارات السليمة.
            if (status.IsSubmitted && status.Status == ExamStatus.Completed && !string.IsNullOrEmpty(status.Note))
                return;

            // ======================================================
            // 2) تحميل الأسئلة (SAFE FALLBACK)
            // ======================================================
            List<(Guid QuestionId, int SectionId)> questionRows;

            // OLD SYSTEM
            var oldRows = await (
                from eq in db.ExamQuestions
                join q in db.Questions on eq.QuestionId equals q.Id
                where
                    (!isIndividual && eq.ExamAssignmentId == effectiveAssignmentId) ||
                    (isIndividual && eq.ExamAssignmentToStudentId == effectiveAssignmentId)
                select new
                {
                    eq.QuestionId,
                    SectionId = q.SectionId ?? 0
                }
            ).ToListAsync();

            if (oldRows.Any())
            {
                questionRows = oldRows
                    .Select(x => (x.QuestionId, x.SectionId))
                    .ToList();
            }
            else
            {
                int examId;

                if (isIndividual)
                {
                    examId = await db.ExamAssignmentsToStudents
                        .Where(a => a.Id == effectiveAssignmentId)
                        .Select(a => a.ExamId)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    examId = (await db.ExamAssignmentsToBatches
      .Where(a => a.Id == effectiveAssignmentId)
      .Select(a => a.ExamId)
      .FirstOrDefaultAsync()) ?? 0;
                }

                var fallbackRows = await (
                    from eq in db.ExamQuestions
                    join q in db.Questions on eq.QuestionId equals q.Id
                    where eq.ExamId == examId
                    select new
                    {
                        eq.QuestionId,
                        SectionId = q.SectionId ?? 0
                    }
                ).ToListAsync();

                questionRows = fallbackRows
                    .Select(x => (x.QuestionId, x.SectionId))
                    .ToList();

                // SECOND FALLBACK: قد تكون بنك أسئلة الاختبار (ExamQuestions) قد
                // عُدِّل بعد أن بدأ الطالب محاولته. المرجع الحقيقي لما شاهده وحلّه
                // الطالب فعليًا هو اللقطة المحفوظة له عند بدء الاختبار
                // (ExamStudentQuestionOrders)، فنستخدمها بدلاً من رمي خطأ.
                if (!questionRows.Any())
                {
                    var snapshotRows = await (
                        from o in db.ExamStudentQuestionOrders
                        join q in db.Questions on o.QuestionId equals q.Id
                        where
                            o.StudentId == context.StudentId &&
                            (
                                (!isIndividual && o.ExamAssignmentId == effectiveAssignmentId) ||
                                (isIndividual && o.ExamAssignmentToStudentId == effectiveAssignmentId)
                            )
                        select new
                        {
                            o.QuestionId,
                            SectionId = q.SectionId ?? 0
                        }
                    ).ToListAsync();

                    questionRows = snapshotRows
                        .Select(x => (x.QuestionId, x.SectionId))
                        .ToList();
                }

                // THIRD FALLBACK: بعض تدفقات الاختبار الفردي لا تكتب أي لقطة
                // (لا في ExamQuestions ولا في ExamStudentQuestionOrders) —
                // تعتمد فقط على الجلسة (Session) لعرض الأسئلة. في هذه الحالة
                // السجل الوحيد الباقي في القاعدة لما حلّه الطالب فعليًا هو
                // محاولاته المحفوظة (QuestionAttemptNew)، فنبني منها قائمة
                // الأسئلة بدلاً من رمي خطأ ومنعه من إنهاء اختبار أجاب عليه بالكامل.
                if (!questionRows.Any())
                {
                    var attemptedRows = await (
                        from a in db.QuestionAttemptNew
                        join q in db.Questions on a.QuestionId equals q.Id
                        where
                            a.StudentId == context.StudentId &&
                            (
                                (!isIndividual && a.ExamAssignmentId == effectiveAssignmentId) ||
                                (isIndividual && a.ExamAssignmentToStudentId == effectiveAssignmentId)
                            )
                        select new
                        {
                            a.QuestionId,
                            SectionId = q.SectionId ?? 0
                        }
                    ).Distinct().ToListAsync();

                    questionRows = attemptedRows
                        .Select(x => (x.QuestionId, x.SectionId))
                        .ToList();
                }
            }

            if (!questionRows.Any())
                throw new Exception("لا توجد أسئلة مرتبطة بهذا الاختبار.");

            int totalQuestions = questionRows.Count;

            var questionSectionMap = questionRows
                .GroupBy(x => x.QuestionId)
                .Select(g => g.First())
                .ToDictionary(x => x.QuestionId, x => x.SectionId);

            // ======================================================
            // 3) تحميل المحاولات
            // ======================================================
            var attempts = await db.QuestionAttemptNew
                .Where(a =>
                    a.StudentId == context.StudentId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == effectiveAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == effectiveAssignmentId)
                    ))
                .ToListAsync();

            var lastAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .Where(a => questionSectionMap.ContainsKey(a.QuestionId))
                .ToList();

            int orphanedAttemptsCount = attempts
                .Select(a => a.QuestionId).Distinct()
                .Count(qid => !questionSectionMap.ContainsKey(qid));

            // ======================================================
            // 4) الحساب
            // ======================================================
            int totalTimeSeconds = attempts.Sum(a =>
                a.TimeTakenSeconds > 0 ? (int)Math.Round(a.TimeTakenSeconds) : 0);

            int answeredCount = lastAttempts.Count;
            int correctCount = lastAttempts.Count(a => a.IsCorrect);
            int wrongCount = lastAttempts.Count(a =>
                !a.IsCorrect && !string.IsNullOrWhiteSpace(a.SelectedAnswer));

            int skippedCount = Math.Max(0, totalQuestions - answeredCount);

            if (answeredCount > totalQuestions || orphanedAttemptsCount > 0)
            {
                _logger.LogWarning(
                    "ExamResultEngine: تعارض بيانات لاختبار Assignment={AssignmentId} طالب={StudentId} — " +
                    "إجمالي أسئلة حالية={TotalQuestions}, محاولات مرتبطة إجمالاً={AllAttempts}, " +
                    "محاولات يتيمة (سؤال لم يعد ضمن الاختبار)={Orphaned}.",
                    effectiveAssignmentId, context.StudentId, totalQuestions, attempts.Count, orphanedAttemptsCount);
            }

            double scorePercent = totalQuestions > 0
                ? Math.Min(100, Math.Round(correctCount * 100.0 / totalQuestions, 1))
                : 0;

            // ======================================================
            // 5) التحليل حسب المحاور
            // ======================================================
            var sectionResults = new Dictionary<int, SectionResultDto>();

            foreach (var attempt in lastAttempts)
            {
                if (!questionSectionMap.TryGetValue(attempt.QuestionId, out int sectionId))
                    sectionId = 0;

                if (!sectionResults.ContainsKey(sectionId))
                    sectionResults[sectionId] = new SectionResultDto();

                if (attempt.IsCorrect)
                    sectionResults[sectionId].Correct++;
                else if (!string.IsNullOrWhiteSpace(attempt.SelectedAnswer))
                    sectionResults[sectionId].Wrong++;
            }

            foreach (var grp in questionRows.GroupBy(q => q.SectionId))
            {
                int sectionId = grp.Key;
                int totalInSection = grp.Count();

                int answeredInSection =
                    sectionResults.TryGetValue(sectionId, out var r)
                        ? r.Correct + r.Wrong
                        : 0;

                if (!sectionResults.ContainsKey(sectionId))
                    sectionResults[sectionId] = new SectionResultDto();

                sectionResults[sectionId].Skipped = totalInSection - answeredInSection;
            }

            // ======================================================
            // 6) حفظ النتيجة
            // ======================================================
            var snapshot = new ExamFinalResultDto
            {
                TotalQuestions = totalQuestions,
                Correct = correctCount,
                Wrong = wrongCount,
                Skipped = skippedCount,
                ScorePercent = scorePercent,
                TotalTimeSeconds = totalTimeSeconds,
                Sections = sectionResults
            };

            int newScore = (int)Math.Round(scorePercent);
            bool isNewBest = !status.Score.HasValue || newScore >= status.Score.Value;

            if (isNewBest)
            {
                status.Score = newScore;
                status.Note = JsonSerializer.Serialize(snapshot);
            }

            status.IsSubmitted = true;
            status.Status = ExamStatus.Completed;
            status.SubmittedAt = DateTime.UtcNow;
            status.ReviewSeconds = reviewSeconds;

            await db.SaveChangesAsync();
        }

        public async Task GenerateSnapshotIfMissingAsync(int examAssignmentId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var status = await db.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == examAssignmentId);

            if (status == null || !string.IsNullOrEmpty(status.Note))
                return; // Snapshot موجود أو لا توجد حالة

            // ⚠️ هذا الحساب يتم مرة واحدة فقط
            var context = new ExamResultContext
            {
                ExamAssignmentId = examAssignmentId,
                StudentId = studentId,
                Kind = ExamKind.General
            };

            // reuse نفس المنطق القديم لكن داخل Engine
            await FinalizeAsync(context, reviewSeconds: 0);
        }

        public async Task GenerateSnapshotIfMissingForIndividualAsync(int examAssignmentToStudentId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var status = await db.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentToStudentId == examAssignmentToStudentId);

            if (status == null || !string.IsNullOrEmpty(status.Note))
                return;

            var context = new ExamResultContext
            {
                ExamAssignmentToStudentId = examAssignmentToStudentId,
                StudentId = studentId,
                Kind = ExamKind.General
            };

            await FinalizeAsync(context, reviewSeconds: 0);
        }




    }
}
