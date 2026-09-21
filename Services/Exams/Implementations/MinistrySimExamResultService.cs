using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Exams.Implementations
{
    // Sprint 14 (MSE-H / H1): استخراج حرفي لمنطق Result/QuestionReview من
    // Areas/Students/Controllers/MinistrySimExamController (Sprint 12/13) بلا أي تغيير في قواعد الحساب،
    // فقط استبدال "الطالب الحالي من الجلسة" بـ studentId صريح يُمرَّر من المستدعي.
    public class MinistrySimExamResultService : IMinistrySimExamResultService
    {
        private readonly ApplicationDbContext _context;

        public MinistrySimExamResultService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MinistrySimExamResultLookup> GetResultAsync(int ministrySimExamId, int studentId)
        {
            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Include(a => a.StageProgress)
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == ministrySimExamId && a.StudentId == studentId);

            if (attempt == null)
                return new MinistrySimExamResultLookup { Status = MinistrySimExamResultStatus.AttemptNotFound };

            if (!attempt.IsCompleted)
                return new MinistrySimExamResultLookup { Status = MinistrySimExamResultStatus.NotCompleted };

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
                return new MinistrySimExamResultLookup { Status = MinistrySimExamResultStatus.ExamNotFound };

            var studentName = await _context.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync();

            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .Select(sq => new { sq.Id, sq.MinistrySimExamStage.StageNumber, sq.IsQuant })
                .ToListAsync();

            var stageDurations = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.MinistrySimExamId == ministrySimExamId)
                .Select(s => new { s.StageNumber, s.DurationMinutes })
                .ToDictionaryAsync(s => s.StageNumber, s => s.DurationMinutes);

            var answers = await _context.MinistrySimExamStudentAnswers
                .AsNoTracking()
                .Where(a => a.MinistrySimExamStudentAttemptId == attempt.Id)
                .Select(a => new { a.MinistrySimExamStageQuestionId, a.IsCorrect, a.SelectedOptionId })
                .ToListAsync();

            var answersByStageQuestionId = answers.ToDictionary(a => a.MinistrySimExamStageQuestionId);

            var stages = attempt.StageProgress
                .OrderBy(p => p.StageNumber)
                .Select(p =>
                {
                    var questionsInStage = stageQuestions.Where(sq => sq.StageNumber == p.StageNumber).ToList();
                    var correct = 0;
                    var wrong = 0;
                    foreach (var sq in questionsInStage)
                    {
                        if (!answersByStageQuestionId.TryGetValue(sq.Id, out var answer) || !answer.SelectedOptionId.HasValue)
                            continue;
                        if (answer.IsCorrect == true) correct++;
                        else wrong++;
                    }

                    return new MinistrySimExamStageResultVm
                    {
                        StageNumber = p.StageNumber,
                        StagePercentScore = p.StagePercentScore ?? 0,
                        TimeExpired = p.TimeExpired,
                        DurationMinutes = stageDurations.TryGetValue(p.StageNumber, out var duration) ? duration : 26,
                        QuestionCount = questionsInStage.Count,
                        CorrectCount = correct,
                        WrongCount = wrong,
                        UnansweredCount = questionsInStage.Count - correct - wrong
                    };
                })
                .ToList();

            var quantTotal = stageQuestions.Count(sq => sq.IsQuant);
            var verbalTotal = stageQuestions.Count(sq => !sq.IsQuant);
            var quantCorrect = stageQuestions.Count(sq => sq.IsQuant
                && answersByStageQuestionId.TryGetValue(sq.Id, out var qa) && qa.IsCorrect == true);
            var verbalCorrect = stageQuestions.Count(sq => !sq.IsQuant
                && answersByStageQuestionId.TryGetValue(sq.Id, out var va) && va.IsCorrect == true);
            var quantWrong = stageQuestions.Count(sq => sq.IsQuant
                && answersByStageQuestionId.TryGetValue(sq.Id, out var qw) && qw.SelectedOptionId.HasValue && qw.IsCorrect != true);
            var verbalWrong = stageQuestions.Count(sq => !sq.IsQuant
                && answersByStageQuestionId.TryGetValue(sq.Id, out var vw) && vw.SelectedOptionId.HasValue && vw.IsCorrect != true);

            var vm = new MinistrySimExamResultVm
            {
                MinistrySimExamId = exam.Id,
                ExamTitle = exam.Title,
                StudentId = studentId,
                StudentName = studentName,
                TotalScorePercent = attempt.TotalScorePercent ?? 0,
                CompletedAt = attempt.CompletedAt,
                Stages = stages,
                TotalQuestions = stages.Sum(s => s.QuestionCount),
                TotalCorrect = stages.Sum(s => s.CorrectCount),
                TotalWrong = stages.Sum(s => s.WrongCount),
                TotalUnanswered = stages.Sum(s => s.UnansweredCount),
                QuantTotal = quantTotal,
                VerbalTotal = verbalTotal,
                QuantCorrect = quantCorrect,
                VerbalCorrect = verbalCorrect,
                QuantWrong = quantWrong,
                VerbalWrong = verbalWrong,
                QuantUnanswered = quantTotal - quantCorrect - quantWrong,
                VerbalUnanswered = verbalTotal - verbalCorrect - verbalWrong
            };

            return new MinistrySimExamResultLookup { Status = MinistrySimExamResultStatus.Ok, Vm = vm };
        }

        public async Task<MinistrySimExamQuestionReviewLookup> GetQuestionReviewAsync(int ministrySimExamId, int studentId, int stageNumber)
        {
            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == ministrySimExamId && a.StudentId == studentId);

            if (attempt == null)
                return new MinistrySimExamQuestionReviewLookup { Status = MinistrySimExamQuestionReviewStatus.AttemptNotFound };

            if (!attempt.IsCompleted)
                return new MinistrySimExamQuestionReviewLookup { Status = MinistrySimExamQuestionReviewStatus.NotCompleted };

            var progress = await _context.MinistrySimExamStudentStageProgresses
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == attempt.Id && p.StageNumber == stageNumber);

            if (progress == null)
                return new MinistrySimExamQuestionReviewLookup { Status = MinistrySimExamQuestionReviewStatus.StageNotFound };

            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Include(s => s.QuantSection)
                .Include(s => s.VerbalSection)
                .FirstOrDefaultAsync(s => s.MinistrySimExamId == ministrySimExamId && s.StageNumber == stageNumber);

            if (stage == null)
                return new MinistrySimExamQuestionReviewLookup { Status = MinistrySimExamQuestionReviewStatus.StageNotFound };

            var studentName = await _context.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync();

            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Include(sq => sq.Question).ThenInclude(q => q.Options)
                .Include(sq => sq.Question).ThenInclude(q => q.VerbalPassage)
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId && sq.MinistrySimExamStage.StageNumber == stageNumber)
                .OrderBy(sq => sq.StageOrder)
                .ToListAsync();

            var stageQuestionIds = stageQuestions.Select(sq => sq.Id).ToList();

            var answers = await _context.MinistrySimExamStudentAnswers
                .AsNoTracking()
                .Where(a => a.MinistrySimExamStudentAttemptId == attempt.Id && EF.Constant(stageQuestionIds).Contains(a.MinistrySimExamStageQuestionId))
                .ToListAsync();

            var answersByStageQuestionId = answers.ToDictionary(a => a.MinistrySimExamStageQuestionId);

            var questionItems = stageQuestions.Select(sq =>
            {
                answersByStageQuestionId.TryGetValue(sq.Id, out var answer);

                var options = sq.Question.Options.ToList();
                string studentAnswerText = null;
                if (answer?.SelectedOptionId is int idx && idx >= 0 && idx < options.Count)
                    studentAnswerText = options[idx].Text;

                return new MinistrySimExamQuestionReviewItem
                {
                    QuestionId = sq.QuestionId,
                    GlobalOrder = sq.GlobalOrder,
                    IsQuant = sq.IsQuant,
                    QuestionText = sq.Question.Title,
                    ImageUrl = sq.Question.ImageUrl,
                    IsQuantitative = sq.Question.IsQuantitative,
                    IsCorrect = answer?.IsCorrect,
                    StudentAnswer = studentAnswerText,
                    CorrectAnswer = sq.Question.CorrectAnswer,
                    Explanation = sq.Question.Explanation,
                    VideoUrl = sq.Question.VideoUrl,
                    VerbalPassageTitle = sq.Question.VerbalPassage?.Title,
                    VerbalPassageContent = sq.Question.VerbalPassage?.Content,
                    VerbalPassageMediaUrl = sq.Question.VerbalPassage?.MediaUrl,
                    VerbalPassageType = sq.Question.VerbalPassage != null ? (PassageType?)sq.Question.VerbalPassage.Type : null,
                    ComparisonValue1 = sq.Question.ValueA,
                    ComparisonValue2 = sq.Question.ValueB,
                    IsRTL = true,
                    DisplayType = sq.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    },
                    Options = options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                };
            }).ToList();

            var vm = new MinistrySimExamQuestionReviewVm
            {
                MinistrySimExamId = ministrySimExamId,
                AttemptId = attempt.Id,
                StageNumber = stageNumber,
                StudentId = studentId,
                StudentName = studentName,
                StageAxisSummary = $"المحور الكمي: {stage.QuantSection?.Title} — المحور اللفظي: {stage.VerbalSection?.Title}",
                TimeExpired = progress.TimeExpired,
                TotalQuestions = questionItems.Count,
                CorrectAnswers = questionItems.Count(q => q.IsCorrect == true),
                WrongAnswers = questionItems.Count(q => q.IsCorrect == false),
                SkippedAnswers = questionItems.Count(q => q.IsCorrect == null),
                Questions = questionItems
            };

            return new MinistrySimExamQuestionReviewLookup { Status = MinistrySimExamQuestionReviewStatus.Ok, Vm = vm };
        }
    }
}
