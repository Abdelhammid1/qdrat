using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Interfaces;

namespace QdratNew.Services.Exams.Implementations
{
    // اختبار محاكاة اختبار الوزارة — Sprint 9 (MSE-F): بدء/استئناف المحاولة (StartOrResumeAttemptAsync)،
    // بدء مرحلة (StartStageAsync)، حفظ إجابة (SubmitAnswerAsync).
    // Sprint 10 (MSE-F): LockStageAsync (القفل النهائي أحادي الاتجاه) + EnforceStageTimeLimitAsync (تحقق الخادم من انتهاء الوقت).
    public class MinistrySimExamAttemptService : IMinistrySimExamAttemptService
    {
        private readonly ApplicationDbContext _context;

        public MinistrySimExamAttemptService(ApplicationDbContext context)
        {
            _context = context;
        }

        // يرفض إن توجد محاولة سابقة مكتملة (القرار #7 — محاولة واحدة فقط)، ويرفض إن كان الاختبار غير منشور
        // أو غير مُسنَد فعليًا لهذا الطالب (فرديًا أو عبر دفعة مُسنَد إليها). يُنشئ محاولة جديدة عند أول دخول فقط.
        public async Task<MinistrySimExamStudentAttempt> StartOrResumeAttemptAsync(int ministrySimExamId, int studentId)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
                throw new InvalidOperationException("لم يتم العثور على اختبار معمل القياس المطلوب.");

            if (!exam.IsPublished)
                throw new InvalidOperationException("هذا الاختبار غير منشور بعد.");

            var existingAttempt = await _context.MinistrySimExamStudentAttempts
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == ministrySimExamId && a.StudentId == studentId);

            if (existingAttempt != null)
            {
                if (existingAttempt.IsCompleted)
                    throw new InvalidOperationException("لقد أتممت هذا الاختبار بالفعل — لا يمكن بدء محاولة ثانية.");

                return existingAttempt;
            }

            var assignedDirectly = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .AnyAsync(x => x.MinistrySimExamId == ministrySimExamId && x.StudentId == studentId);

            var assignedViaBatch = false;
            if (!assignedDirectly)
            {
                var assignedBatchIds = await _context.MinistrySimExamAssignmentsToBatches
                    .AsNoTracking()
                    .Where(x => x.MinistrySimExamId == ministrySimExamId)
                    .Select(x => x.BatchId)
                    .ToListAsync();

                if (assignedBatchIds.Any())
                {
                    assignedViaBatch = await _context.StudentBatchEnrollments
                        .AsNoTracking()
                        .AnyAsync(e => e.StudentID == studentId
                                       && e.Status == "Active"
                                       && EF.Constant(assignedBatchIds).Contains(e.BatchId));
                }
            }

            if (!assignedDirectly && !assignedViaBatch)
                throw new InvalidOperationException("هذا الاختبار غير مُسنَد إليك.");

            var attempt = new MinistrySimExamStudentAttempt
            {
                MinistrySimExamId = ministrySimExamId,
                StudentId = studentId,
                StartedAt = DateTime.Now,
                IsCompleted = false
            };

            _context.MinistrySimExamStudentAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return attempt;
        }

        // يرفض إن كانت مرحلة سابقة (StageNumber أقل) غير مقفلة بعد، أو إن كانت هذه المرحلة نفسها مقفلة أصلاً.
        // لو المرحلة بدأت أصلاً ولم تُقفل بعد (تحديث صفحة مثلاً) يعيد نفس صف التقدم دون إنشاء صف جديد.
        public async Task<MinistrySimExamStudentStageProgress> StartStageAsync(int attemptId, int stageNumber)
        {
            var attempt = await _context.MinistrySimExamStudentAttempts
                .Include(a => a.StageProgress)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
                throw new InvalidOperationException("لم يتم العثور على محاولة الطالب المطلوبة.");

            if (attempt.IsCompleted)
                throw new InvalidOperationException("لقد أتممت هذا الاختبار بالفعل.");

            var stageExists = await _context.MinistrySimExamStages
                .AsNoTracking()
                .AnyAsync(s => s.MinistrySimExamId == attempt.MinistrySimExamId && s.StageNumber == stageNumber);

            if (!stageExists)
                throw new InvalidOperationException("لم يتم العثور على هذه المرحلة ضمن الاختبار.");

            for (int n = 1; n < stageNumber; n++)
            {
                var prior = attempt.StageProgress.FirstOrDefault(p => p.StageNumber == n);
                if (prior == null || !prior.IsLocked)
                    throw new InvalidOperationException("يجب إنهاء المراحل السابقة بالترتيب قبل بدء هذه المرحلة.");
            }

            var existing = attempt.StageProgress.FirstOrDefault(p => p.StageNumber == stageNumber);
            if (existing != null)
            {
                if (existing.IsLocked)
                    throw new InvalidOperationException("هذه المرحلة مقفلة بالفعل ولا يمكن الدخول إليها مرة أخرى.");

                return existing;
            }

            var progress = new MinistrySimExamStudentStageProgress
            {
                MinistrySimExamStudentAttemptId = attemptId,
                StageNumber = stageNumber,
                StartedAt = DateTime.Now,
                IsLocked = false
            };

            _context.MinistrySimExamStudentStageProgresses.Add(progress);
            await _context.SaveChangesAsync();

            return progress;
        }

        // يحفظ/يحدّث إجابة الطالب على سؤال واحد ضمن مرحلة مفتوحة (غير مقفلة) فقط. يحسب IsCorrect فورًا بمقارنة
        // نص الخيار المختار (حسب فهرسه ضمن Question.Options) بـ Question.CorrectAnswer.
        public async Task SubmitAnswerAsync(int attemptId, int stageQuestionId, int? selectedOptionId, bool flagForReview)
        {
            var attempt = await _context.MinistrySimExamStudentAttempts
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
                throw new InvalidOperationException("لم يتم العثور على محاولة الطالب المطلوبة.");

            if (attempt.IsCompleted)
                throw new InvalidOperationException("لقد أتممت هذا الاختبار بالفعل — لا يمكن تعديل الإجابات.");

            var stageQuestion = await _context.MinistrySimExamStageQuestions
                .Include(sq => sq.MinistrySimExamStage)
                .Include(sq => sq.Question).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(sq => sq.Id == stageQuestionId);

            if (stageQuestion == null || stageQuestion.MinistrySimExamStage.MinistrySimExamId != attempt.MinistrySimExamId)
                throw new InvalidOperationException("هذا السؤال لا ينتمي لاختبارك الحالي.");

            var progress = await _context.MinistrySimExamStudentStageProgresses
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == attemptId
                                           && p.StageNumber == stageQuestion.MinistrySimExamStage.StageNumber);

            if (progress == null || progress.IsLocked)
                throw new InvalidOperationException("هذه المرحلة غير مفتوحة حاليًا — لا يمكن حفظ الإجابة.");

            bool? isCorrect = null;
            if (selectedOptionId.HasValue)
            {
                var options = stageQuestion.Question.Options.ToList();
                if (selectedOptionId.Value >= 0 && selectedOptionId.Value < options.Count)
                {
                    var selectedText = options[selectedOptionId.Value].Text;
                    isCorrect = string.Equals(
                        selectedText?.Trim(),
                        stageQuestion.Question.CorrectAnswer?.Trim(),
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            var answer = await _context.MinistrySimExamStudentAnswers
                .FirstOrDefaultAsync(a => a.MinistrySimExamStudentAttemptId == attemptId
                                           && a.MinistrySimExamStageQuestionId == stageQuestionId);

            if (answer == null)
            {
                answer = new MinistrySimExamStudentAnswer
                {
                    MinistrySimExamStudentAttemptId = attemptId,
                    MinistrySimExamStageQuestionId = stageQuestionId,
                    SelectedOptionId = selectedOptionId,
                    IsCorrect = isCorrect,
                    AnsweredAt = DateTime.Now,
                    IsFlaggedForReview = flagForReview
                };
                _context.MinistrySimExamStudentAnswers.Add(answer);
            }
            else
            {
                answer.SelectedOptionId = selectedOptionId;
                answer.IsCorrect = isCorrect;
                answer.AnsweredAt = DateTime.Now;
                answer.IsFlaggedForReview = flagForReview;
            }

            await _context.SaveChangesAsync();
        }

        // القفل النهائي أحادي الاتجاه (F4). لا يوجد أي مسار كود بعد هذه الدالة يعيد IsLocked إلى false.
        // Idempotent عمدًا: استدعاء مزدوج (مثلاً طلب "إنهاء المرحلة" من الطالب يتزامن مع فحص انتهاء الوقت من طلب آخر)
        // يُعيد نفس الصف المقفل بالفعل دون خطأ، بدل فشل أحد الطلبين بسباق تحديثات.
        // Sprint 12 (G1): يحسب StagePercentScore لهذه المرحلة فور قفلها، وTotalScorePercent للمحاولة كاملة عند قفل آخر مرحلة.
        public async Task<MinistrySimExamStudentStageProgress> LockStageAsync(int attemptId, int stageNumber, bool timeExpired)
        {
            var progress = await _context.MinistrySimExamStudentStageProgresses
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == attemptId && p.StageNumber == stageNumber);

            if (progress == null)
                throw new InvalidOperationException("لم يتم العثور على تقدّم هذه المرحلة — لم تبدأ بعد.");

            if (progress.IsLocked)
                return progress;

            var attempt = await _context.MinistrySimExamStudentAttempts
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
                throw new InvalidOperationException("لم يتم العثور على محاولة الطالب المطلوبة.");

            progress.IsLocked = true;
            progress.EndAt = DateTime.Now;
            progress.TimeExpired = timeExpired;

            // G1: نتيجة المرحلة = نسبة الإجابات الصحيحة من إجمالي أسئلة المرحلة (السؤال بلا إجابة يُحتسب ضمن المقام فقط)
            var stageQuestionCount = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .CountAsync(sq => sq.MinistrySimExamStage.MinistrySimExamId == attempt.MinistrySimExamId
                                   && sq.MinistrySimExamStage.StageNumber == stageNumber);

            var stageCorrectCount = await _context.MinistrySimExamStudentAnswers
                .AsNoTracking()
                .CountAsync(a => a.MinistrySimExamStudentAttemptId == attemptId
                                  && a.IsCorrect == true
                                  && a.StageQuestion.MinistrySimExamStage.MinistrySimExamId == attempt.MinistrySimExamId
                                  && a.StageQuestion.MinistrySimExamStage.StageNumber == stageNumber);

            progress.StagePercentScore = stageQuestionCount > 0
                ? Math.Round(stageCorrectCount * 100.0 / stageQuestionCount, 2)
                : 0;

            await _context.SaveChangesAsync();

            // قفل آخر مرحلة يُنهي المحاولة بالكامل ويحسب النتيجة الإجمالية — جزء من نفس دورة حياة القفل أحادي
            // الاتجاه (لا مسار آخر يضبط IsCompleted/TotalScorePercent)
            if (!attempt.IsCompleted)
            {
                var exam = await _context.MinistrySimExams
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == attempt.MinistrySimExamId);

                if (exam != null && stageNumber >= exam.TotalStages)
                {
                    var totalQuestionCount = await _context.MinistrySimExamStageQuestions
                        .AsNoTracking()
                        .CountAsync(sq => sq.MinistrySimExamStage.MinistrySimExamId == attempt.MinistrySimExamId);

                    var totalCorrectCount = await _context.MinistrySimExamStudentAnswers
                        .AsNoTracking()
                        .CountAsync(a => a.MinistrySimExamStudentAttemptId == attemptId && a.IsCorrect == true);

                    attempt.IsCompleted = true;
                    attempt.CompletedAt = DateTime.Now;
                    attempt.TotalScorePercent = totalQuestionCount > 0
                        ? Math.Round(totalCorrectCount * 100.0 / totalQuestionCount, 2)
                        : 0;
                    await _context.SaveChangesAsync();
                }
            }

            return progress;
        }

        // Sprint 10 (F3): الحسم النهائي لانتهاء الوقت يكون هنا فقط، بمقارنة وقت الخادم — لا ثقة بأي مؤقت Client.
        public async Task<bool> EnforceStageTimeLimitAsync(int attemptId, int stageNumber)
        {
            var progress = await _context.MinistrySimExamStudentStageProgresses
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == attemptId && p.StageNumber == stageNumber);

            if (progress == null || progress.IsLocked || !progress.StartedAt.HasValue)
                return false;

            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
                return false;

            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.MinistrySimExamId == attempt.MinistrySimExamId && s.StageNumber == stageNumber);

            if (stage == null)
                return false;

            var deadline = progress.StartedAt.Value.AddMinutes(stage.DurationMinutes);
            if (DateTime.Now < deadline)
                return false;

            await LockStageAsync(attemptId, stageNumber, timeExpired: true);
            return true;
        }

        // Sprint 17 (MSE-J / J3): مصدر الحقيقة الوحيد هو MinistrySimExamAssignmentToStudent — سواء إسناد فردي
        // مباشر (SourceType = Official) أو إسناد آتٍ أصلاً من دفعة (SourceType = Batch، يُنشَأ في
        // MinistrySimExamAssignmentService.AssignStudentsInternalAsync بنفس IsOnline/ReferenceCode لدفعته وقت
        // إسنادها). هذا يمنع ازدواج الفحص بين جدولي الدفعة والطالب لكل طالب مُسنَد عبر دفعة.
        public async Task<bool> ValidateReferenceCodeAsync(int ministrySimExamId, int studentId, string enteredCode)
        {
            var assignment = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MinistrySimExamId == ministrySimExamId && x.StudentId == studentId);

            // لا يوجد إسناد فعلي لهذا الطالب — تُترك مهمة رفض الوصول لـ StartOrResumeAttemptAsync لاحقًا
            if (assignment == null)
                return true;

            if (assignment.IsOnline)
                return true;

            return !string.IsNullOrWhiteSpace(enteredCode)
                && string.Equals(assignment.ReferenceCode?.Trim(), enteredCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
