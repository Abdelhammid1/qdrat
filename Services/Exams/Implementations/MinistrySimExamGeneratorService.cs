using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Implementations
{
    // اختبار محاكاة اختبار الوزارة — Sprint 4 (MSE-C): محرك التوليد + الترقيم التراكمي
    // Sprint 5 (MSE-C / C3): ValidateExactCountsAsync — بوابة النشر الصارمة
    public class MinistrySimExamGeneratorService : IMinistrySimExamGeneratorService
    {
        private readonly ApplicationDbContext _context;

        public MinistrySimExamGeneratorService(ApplicationDbContext context)
        {
            _context = context;
        }

        // C1: يُنفَّذ عند الضغط على "بناء الأسئلة" لمرحلة كاملة — يستبدل أسئلة المرحلة بالكامل بمجموعة جديدة
        // مسحوبة من بنك الأسئلة العام بلا أي تكملة صامتة للنقص (القرار الملزم رقم 2 بالملف التنفيذي، القسم 2).
        public async Task<MinistrySimExamGenerationResult> GenerateStageQuestionsAsync(
            int stageId,
            List<IndicatorSelectionInput> quantSelections,
            List<IndicatorSelectionInput> verbalSelections,
            List<Guid> excludeQuestionIds = null)
        {
            quantSelections ??= new List<IndicatorSelectionInput>();
            verbalSelections ??= new List<IndicatorSelectionInput>();
            excludeQuestionIds ??= new List<Guid>();

            if (!quantSelections.Any() && !verbalSelections.Any())
                throw new InvalidOperationException("يجب اختيار مؤشر واحد على الأقل (كمي أو لفظي) قبل بناء الأسئلة.");

            var stage = await _context.MinistrySimExamStages
                .FirstOrDefaultAsync(s => s.Id == stageId);

            if (stage == null)
                throw new InvalidOperationException("لم يتم العثور على قسم اختبار معمل القياس المطلوب.");

            // عناوين كل المحاور المعنيّة (الأساسي + الإضافية) في استعلام واحد
            var sectionIds = quantSelections.Concat(verbalSelections)
                .Select(s => s.SectionId)
                .Concat(new[] { stage.QuantSectionId, stage.VerbalSectionId })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var sectionTitles = await _context.Sections
                .AsNoTracking()
                .Where(s => EF.Constant(sectionIds).Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(s => s.Id, s => s.Title);

            var result = new MinistrySimExamGenerationResult { IsSuccess = true };
            var newStageQuestions = new List<MinistrySimExamStageQuestion>();
            var usedThisCall = new HashSet<Guid>(excludeQuestionIds);

            // منع التكرار على مستوى الاختبار كله: أسئلة بقية الأقسام تدخل في المقارنة، وأسئلة هذا القسم تُستبدل
            var similarityThreshold = await GetSimilarityThresholdAsync();
            var selectedInExam = (await LoadExamQuestionTextsAsync(stage.MinistrySimExamId))
                .Where(q => q.StageNumber != stage.StageNumber)
                .ToList();

            foreach (var question in selectedInExam)
                usedThisCall.Add(question.Id);

            await SelectAxisQuestionsAsync(stage, quantSelections, stage.QuantSectionId, sectionTitles, "المحور الكمي", isQuant: true, newStageQuestions, usedThisCall, selectedInExam, similarityThreshold, result);
            await SelectAxisQuestionsAsync(stage, verbalSelections, stage.VerbalSectionId, sectionTitles, "المحور اللفظي", isQuant: false, newStageQuestions, usedThisCall, selectedInExam, similarityThreshold, result);

            // إعادة البناء الكاملة لأسئلة المرحلة (لا دمج جزئي عند الضغط على "بناء الأسئلة" مجددًا)
            var oldQuestions = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == stageId)
                .ToListAsync();

            if (oldQuestions.Any())
                _context.MinistrySimExamStageQuestions.RemoveRange(oldQuestions);

            // حفظ حذف الأسئلة القديمة + تحديث/إضافة صفوف اختيار المؤشرات (RequestedCount) قبل الإدراج الجماعي
            await _context.SaveChangesAsync();

            // C2: StageOrder داخل المرحلة (كمي أولاً ثم لفظي، بترتيب الإدخال)
            for (int i = 0; i < newStageQuestions.Count; i++)
                newStageQuestions[i].StageOrder = i + 1;

            if (newStageQuestions.Any())
                await _context.BulkInsertAsync(newStageQuestions);

            // C2: GlobalOrder التراكمي عبر المراحل الخمس كاملة (1..120)، محسوب دائمًا بترتيب المراحل 1→5 بلا فجوات
            await RecalculateGlobalOrderAsync(stage.MinistrySimExamId);

            return result;
        }

        private async Task SelectAxisQuestionsAsync(
            MinistrySimExamStage stage,
            List<IndicatorSelectionInput> selections,
            int primarySectionId,
            Dictionary<int, string> sectionTitles,
            string fallbackSectionTitle,
            bool isQuant,
            List<MinistrySimExamStageQuestion> newStageQuestions,
            HashSet<Guid> usedThisCall,
            List<MinistrySimExamQuestionText> selectedInExam,
            int similarityThreshold,
            MinistrySimExamGenerationResult result)
        {
            if (!selections.Any())
                return;

            var lessonIds = selections.Select(s => s.LessonId).Distinct().ToList();

            // ⚠️ EF Core 8 يترجم List<T>.Contains() افتراضيًا عبر OPENJSON — غير مدعوم على SQL Server 2014
            // (Compatibility Level 120 — راجع AGENTS.md § "Avoid OPENJSON-generating queries").
            // EF.Constant() يجبر الترجمة على IN (...) حرفية بدل OPENJSON، لكل استعلام على حدة كما هو موصى به في تعليق Program.cs.
            var lessonTitles = await _context.Lessons
                .AsNoTracking()
                .Where(l => EF.Constant(lessonIds).Contains(l.Id))
                .Select(l => new { l.Id, l.Title })
                .ToListAsync();

            // نفس شروط جودة السؤال المستخدمة في ExamQuestionSelectorService — بدون أي تكملة تلقائية للنقص من مصدر آخر
            var candidateQuestions = await _context.Questions
                .AsNoTracking()
                .Where(q => EF.Constant(lessonIds).Contains(q.LessonId)
                            && q.IsReviewed
                            && !q.IsRejected
                            && q.CorrectAnswer != null)
                .Select(q => new MinistrySimExamQuestionText
                {
                    Id = q.Id,
                    LessonId = q.LessonId,
                    Difficulty = q.Difficulty,
                    VerbalPassageId = q.VerbalPassageId,
                    ImageUrl = q.ImageUrl,
                    Title = q.Title,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    CorrectAnswer = q.CorrectAnswer
                })
                .ToListAsync();

            // كل صفوف اختيار المؤشرات الحالية لهذه المرحلة تُجلَب مرة واحدة (لا استعلام داخل الحلقة)
            var existingSelections = await _context.MinistrySimExamStageIndicatorSelections
                .Where(x => x.MinistrySimExamStageId == stage.Id && EF.Constant(lessonIds).Contains(x.LessonId))
                .ToListAsync();

            var rnd = new Random();

            foreach (var sel in selections)
            {
                var pool = candidateQuestions
                    .Where(q => q.LessonId == sel.LessonId
                                && q.Difficulty == sel.Difficulty
                                && !usedThisCall.Contains(q.Id))
                    .OrderBy(_ => rnd.Next())
                    .ToList();

                var (take, rejectedAsSimilar) = PickDistinctQuestions(pool, sel.RequestedCount, selectedInExam, similarityThreshold);

                foreach (var q in take)
                {
                    usedThisCall.Add(q.Id);
                    newStageQuestions.Add(new MinistrySimExamStageQuestion
                    {
                        MinistrySimExamStageId = stage.Id,
                        QuestionId = q.Id,
                        IsQuant = isQuant,
                        IsManuallySelected = false
                    });
                }

                // SectionId == 0 → المحور الأساسي للفرع؛ غير ذلك = محور إضافي داخل نفس الفرع
                var sectionId = sel.SectionId > 0 ? sel.SectionId : primarySectionId;
                var sectionTitle = sectionTitles.TryGetValue(sectionId, out var title) ? title : fallbackSectionTitle;

                UpsertIndicatorSelection(existingSelections, stage.Id, sectionId, isQuant, sel);

                if (take.Count < sel.RequestedCount)
                {
                    var lessonTitle = lessonTitles.FirstOrDefault(l => l.Id == sel.LessonId)?.Title ?? $"مؤشر #{sel.LessonId}";
                    var shortfall = sel.RequestedCount - take.Count;

                    result.IsSuccess = false;
                    result.ShortfallMessages.Add(
                        $"{QdratNew.Helpers.SectionNameHelper.Name(stage.StageNumber)} - المحور «{sectionTitle}» - المؤشر «{lessonTitle}» - صعوبة {DifficultyArabic(sel.Difficulty)}: " +
                        $"مطلوب {sel.RequestedCount} سؤال وناقص {shortfall} (المتاح فعليًا في البنك {take.Count}{SimilarityNote(rejectedAsSimilar)}).");
                }
            }
        }

        // يأخذ من المرشحين (بترتيبهم العشوائي) حتى يكتمل العدد، متخطيًا أي سؤال يشبه ما اختير في الاختبار كله
        private static (List<MinistrySimExamQuestionText> Picked, int RejectedAsSimilar) PickDistinctQuestions(
            List<MinistrySimExamQuestionText> pool,
            int requestedCount,
            List<MinistrySimExamQuestionText> selectedInExam,
            int similarityThreshold)
        {
            var picked = new List<MinistrySimExamQuestionText>();
            var rejected = 0;

            foreach (var candidate in pool)
            {
                if (picked.Count >= requestedCount)
                    break;

                if (selectedInExam.Any(selected => MinistrySimExamQuestionSimilarity.IsDuplicate(candidate, selected, similarityThreshold)))
                {
                    rejected++;
                    continue;
                }

                picked.Add(candidate);
                selectedInExam.Add(candidate);
            }

            return (picked, rejected);
        }

        private static string SimilarityNote(int rejectedAsSimilar) => rejectedAsSimilar > 0
            ? $" بعد استبعاد {rejectedAsSimilar} سؤال مكرر أو مشابه لأسئلة الاختبار"
            : string.Empty;

        private async Task<int> GetSimilarityThresholdAsync()
        {
            var value = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.Key == MinistrySimExamQuestionSimilarity.ThresholdSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return MinistrySimExamQuestionSimilarity.ParseThreshold(value);
        }

        // كل أسئلة الاختبار في استعلام واحد (إسقاط للحقول اللازمة فقط)، بترتيب الأقسام
        private async Task<List<MinistrySimExamQuestionText>> LoadExamQuestionTextsAsync(int ministrySimExamId)
        {
            return await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .OrderBy(sq => sq.MinistrySimExamStage.StageNumber)
                .ThenBy(sq => sq.StageOrder)
                .Select(sq => new MinistrySimExamQuestionText
                {
                    Id = sq.QuestionId,
                    LessonId = sq.Question.LessonId,
                    Difficulty = sq.Question.Difficulty,
                    VerbalPassageId = sq.Question.VerbalPassageId,
                    ImageUrl = sq.Question.ImageUrl,
                    Title = sq.Question.Title,
                    ValueA = sq.Question.ValueA,
                    ValueB = sq.Question.ValueB,
                    CorrectAnswer = sq.Question.CorrectAnswer,
                    StageQuestionId = sq.Id,
                    StageNumber = sq.MinistrySimExamStage.StageNumber,
                    StageOrder = sq.StageOrder
                })
                .ToListAsync();
        }

        private static string DescribePosition(MinistrySimExamQuestionText question)
            => $"{QdratNew.Helpers.SectionNameHelper.Name(question.StageNumber)} - السؤال {question.StageOrder}";

        public async Task<string> FindDuplicateConflictAsync(int ministrySimExamId, Guid questionId, int? ignoreStageQuestionId = null)
        {
            var candidate = await _context.Questions
                .AsNoTracking()
                .Where(q => q.Id == questionId)
                .Select(q => new MinistrySimExamQuestionText
                {
                    Id = q.Id,
                    VerbalPassageId = q.VerbalPassageId,
                    ImageUrl = q.ImageUrl,
                    Title = q.Title,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    CorrectAnswer = q.CorrectAnswer
                })
                .FirstOrDefaultAsync();

            if (candidate == null)
                return null;

            var threshold = await GetSimilarityThresholdAsync();
            var examQuestions = await LoadExamQuestionTextsAsync(ministrySimExamId);

            foreach (var existing in examQuestions.Where(q => q.StageQuestionId != ignoreStageQuestionId))
            {
                if (existing.Id == candidate.Id)
                    return $"هذا السؤال مستخدم بالفعل في هذا الاختبار ({DescribePosition(existing)}).";

                if (MinistrySimExamQuestionSimilarity.IsDuplicate(candidate, existing, threshold))
                    return $"هذا السؤال يشبه سؤالًا موجودًا في الاختبار بنسبة {MinistrySimExamQuestionSimilarity.ScorePercent(candidate, existing)}% ({DescribePosition(existing)}).";
            }

            return null;
        }

        // فحص بوابة النشر: أزواج الأسئلة المكررة أو المتشابهة في الاختبار كله
        private static List<string> FindDuplicatesInExam(List<MinistrySimExamQuestionText> questions, int threshold)
        {
            const int MaxMessages = 20;

            var messages = new List<string>();
            var total = 0;

            for (var i = 0; i < questions.Count; i++)
            {
                for (var j = i + 1; j < questions.Count; j++)
                {
                    if (!MinistrySimExamQuestionSimilarity.IsDuplicate(questions[i], questions[j], threshold))
                        continue;

                    total++;
                    if (messages.Count >= MaxMessages)
                        continue;

                    messages.Add(questions[i].Id == questions[j].Id
                        ? $"سؤال مكرر: {DescribePosition(questions[i])} هو نفسه {DescribePosition(questions[j])}."
                        : $"سؤالان متشابهان بنسبة {MinistrySimExamQuestionSimilarity.ScorePercent(questions[i], questions[j])}%: {DescribePosition(questions[i])} و{DescribePosition(questions[j])}.");
                }
            }

            if (total > MaxMessages)
                messages.Add($"وتوجد {total - MaxMessages} حالة تكرار أخرى لم تُعرض.");

            return messages;
        }

        private void UpsertIndicatorSelection(
            List<MinistrySimExamStageIndicatorSelection> existingSelections,
            int stageId,
            int sectionId,
            bool isQuant,
            IndicatorSelectionInput sel)
        {
            var existing = existingSelections.FirstOrDefault(x => x.LessonId == sel.LessonId && x.Difficulty == sel.Difficulty);

            if (existing == null)
            {
                var added = new MinistrySimExamStageIndicatorSelection
                {
                    MinistrySimExamStageId = stageId,
                    SectionId = sectionId,
                    IsQuant = isQuant,
                    LessonId = sel.LessonId,
                    Difficulty = sel.Difficulty,
                    RequestedCount = sel.RequestedCount
                };
                _context.MinistrySimExamStageIndicatorSelections.Add(added);
                existingSelections.Add(added);
            }
            else
            {
                existing.SectionId = sectionId;
                existing.IsQuant = isQuant;
                existing.RequestedCount = sel.RequestedCount;
            }
        }

        // C2: يُعاد حسابها بالكامل بعد أي توليد لضمان ترقيم تراكمي متصل (1..120) بترتيب المراحل 1→5 دون فجوات،
        // بغض النظر عن ترتيب بناء الأدمن للمراحل أو عدد الأسئلة الفعلي في كل مرحلة حتى تلك اللحظة.
        private async Task RecalculateGlobalOrderAsync(int ministrySimExamId)
        {
            var allQuestions = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .OrderBy(q => q.MinistrySimExamStage.StageNumber)
                .ThenBy(q => q.StageOrder)
                .ToListAsync();

            var updated = new List<MinistrySimExamStageQuestion>();
            int order = 1;

            foreach (var q in allQuestions)
            {
                if (q.GlobalOrder != order)
                {
                    q.GlobalOrder = order;
                    updated.Add(q);
                }
                order++;
            }

            if (updated.Any())
                await _context.BulkUpdateAsync(updated);
        }

        private static string DifficultyArabic(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyLevel.Easy => "سهل",
            DifficultyLevel.Medium => "متوسط",
            DifficultyLevel.Hard => "صعب",
            DifficultyLevel.VeryHard => "صعب جدًا",
            _ => difficulty.ToString()
        };

        // C3: بوابة النشر الصارمة — تُستدعى وجوبًا قبل تعيين IsPublished = true (القرار الملزم رقم 2، ADR-MSE-3).
        // فشل واحد فقط (بمرحلة كاملة أو بمؤشر واحد) يمنع النشر بالكامل — لا نشر جزئي مهما كان صغيرًا.
        public async Task<MinistrySimExamValidationResult> ValidateExactCountsAsync(int ministrySimExamId)
        {
            var result = new MinistrySimExamValidationResult { IsValid = true };

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
            {
                result.IsValid = false;
                result.ShortfallMessages.Add("لم يتم العثور على اختبار معمل القياس المطلوب.");
                return result;
            }

            var stages = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.MinistrySimExamId == ministrySimExamId)
                .OrderBy(s => s.StageNumber)
                .Select(s => new
                {
                    s.Id,
                    s.StageNumber,
                    s.QuantQuestionCount,
                    s.VerbalQuestionCount,
                    QuantSectionTitle = s.QuantSection.Title,
                    VerbalSectionTitle = s.VerbalSection.Title
                })
                .ToListAsync();

            if (stages.Count != exam.TotalStages)
            {
                result.IsValid = false;
                result.ShortfallMessages.Add(
                    $"الاختبار يحتوي على {stages.Count} قسمًا فقط من أصل {exam.TotalStages} قسمًا مطلوبًا — أكمل بناء كل الأقسام قبل النشر.");
                return result;
            }

            // ⚠️ الفلترة عبر MinistrySimExamId مباشرة (Navigation) بدل List<int>.Contains() على stageIds —
            // لتفادي ترجمة EF Core 8 الافتراضية عبر OPENJSON غير المدعومة على SQL Server 2014 (AGENTS.md § JavaScript/SQL Rules).

            // إجمالي الأسئلة الفعلية لكل محور (كمي/لفظي) بكل مرحلة — استعلام واحد بلا حلقات
            var axisCounts = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(q => q.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .GroupBy(q => new { q.MinistrySimExamStageId, q.IsQuant })
                .Select(g => new { g.Key.MinistrySimExamStageId, g.Key.IsQuant, Count = g.Count() })
                .ToListAsync();

            // عناوين كل محاور كل فرع (الأساسي + الإضافية) لعرضها في رسائل النقص — استعلام واحد
            var axisTitleRows = await _context.MinistrySimExamStageIndicatorSelections
                .AsNoTracking()
                .Where(sel => sel.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .Select(sel => new { sel.MinistrySimExamStageId, sel.IsQuant, Title = sel.Section.Title })
                .Distinct()
                .ToListAsync();

            string AxisTitles(int stageId, bool isQuant, string primaryTitle)
            {
                var titles = axisTitleRows
                    .Where(r => r.MinistrySimExamStageId == stageId && r.IsQuant == isQuant)
                    .Select(r => r.Title)
                    .Distinct()
                    .OrderBy(t => t == primaryTitle ? 0 : 1)
                    .ToList();
                return titles.Count > 1 ? string.Join(" + ", titles) : primaryTitle;
            }

            foreach (var stage in stages)
            {
                var quantActual = axisCounts.FirstOrDefault(a => a.MinistrySimExamStageId == stage.Id && a.IsQuant)?.Count ?? 0;
                var verbalActual = axisCounts.FirstOrDefault(a => a.MinistrySimExamStageId == stage.Id && !a.IsQuant)?.Count ?? 0;

                if (quantActual != stage.QuantQuestionCount)
                {
                    result.IsValid = false;
                    result.ShortfallMessages.Add(
                        $"{QdratNew.Helpers.SectionNameHelper.Name(stage.StageNumber)} - المحور «{AxisTitles(stage.Id, true, stage.QuantSectionTitle)}» (كمي): مطلوب {stage.QuantQuestionCount} سؤال والموجود فعليًا {quantActual}.");
                }

                if (verbalActual != stage.VerbalQuestionCount)
                {
                    result.IsValid = false;
                    result.ShortfallMessages.Add(
                        $"{QdratNew.Helpers.SectionNameHelper.Name(stage.StageNumber)} - المحور «{AxisTitles(stage.Id, false, stage.VerbalSectionTitle)}» (لفظي): مطلوب {stage.VerbalQuestionCount} سؤال والموجود فعليًا {verbalActual}.");
                }
            }

            // اختيارات المؤشرات (RequestedCount) مقابل العدد الفعلي المرتبط بنفس (مرحلة، مؤشر، صعوبة) — تطابق تام لا أكثر ولا أقل
            var indicatorSelections = await _context.MinistrySimExamStageIndicatorSelections
                .AsNoTracking()
                .Where(sel => sel.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId)
                .Select(sel => new
                {
                    sel.MinistrySimExamStageId,
                    sel.LessonId,
                    sel.Difficulty,
                    sel.RequestedCount,
                    LessonTitle = sel.Lesson.Title
                })
                .ToListAsync();

            var indicatorActuals = await (
                from sq in _context.MinistrySimExamStageQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on sq.QuestionId equals q.Id
                where sq.MinistrySimExamStage.MinistrySimExamId == ministrySimExamId
                group q by new { sq.MinistrySimExamStageId, q.LessonId, q.Difficulty } into g
                select new { g.Key.MinistrySimExamStageId, g.Key.LessonId, g.Key.Difficulty, Count = g.Count() }
            ).ToListAsync();

            foreach (var sel in indicatorSelections)
            {
                var actual = indicatorActuals.FirstOrDefault(a =>
                    a.MinistrySimExamStageId == sel.MinistrySimExamStageId
                    && a.LessonId == sel.LessonId
                    && a.Difficulty == sel.Difficulty)?.Count ?? 0;

                if (actual == sel.RequestedCount)
                    continue;

                var stageNumber = stages.First(s => s.Id == sel.MinistrySimExamStageId).StageNumber;
                result.IsValid = false;

                if (actual < sel.RequestedCount)
                {
                    result.ShortfallMessages.Add(
                        $"{QdratNew.Helpers.SectionNameHelper.Name(stageNumber)} - المؤشر «{sel.LessonTitle}» - صعوبة {DifficultyArabic(sel.Difficulty)}: " +
                        $"مطلوب {sel.RequestedCount} وناقص {sel.RequestedCount - actual} (الموجود فعليًا {actual}).");
                }
                else
                {
                    result.ShortfallMessages.Add(
                        $"{QdratNew.Helpers.SectionNameHelper.Name(stageNumber)} - المؤشر «{sel.LessonTitle}» - صعوبة {DifficultyArabic(sel.Difficulty)}: " +
                        $"مطلوب {sel.RequestedCount} فقط لكن الموجود فعليًا {actual} (زيادة {actual - sel.RequestedCount} عن المطلوب).");
                }
            }

            // الاختبار المنشور لا يُعاد فحصه: هذه البوابة تخص النشر فقط، ولا أثر لها على ما أُرسل للطلاب
            if (!exam.IsPublished)
            {
                var threshold = await GetSimilarityThresholdAsync();
                var duplicates = FindDuplicatesInExam(await LoadExamQuestionTextsAsync(ministrySimExamId), threshold);

                if (duplicates.Any())
                {
                    result.IsValid = false;
                    result.ShortfallMessages.AddRange(duplicates);
                }
            }

            return result;
        }

        // Sprint 7 (MSE-D / D3): يستبعد سؤالاً واحدًا من المرحلة — حذف الرابط فقط بلا أي سحب تعويضي تلقائي (القرار #2 بالملف
        // التنفيذي)، يترك المؤشر ناقصًا حتى يقرر الأدمن إعادة التوليد (RegenerateIndicatorAsync) أو الإضافة اليدوية
        // (AddQuestionManuallyAsync)، مع إعادة ترقيم StageOrder/GlobalOrder فورًا للحفاظ على التسلسل التراكمي.
        public async Task RemoveStageQuestionAsync(int stageQuestionId)
        {
            var link = await _context.MinistrySimExamStageQuestions
                .FirstOrDefaultAsync(q => q.Id == stageQuestionId);

            if (link == null)
                throw new InvalidOperationException("لم يتم العثور على السؤال المطلوب استبعاده من هذا القسم.");

            var stageId = link.MinistrySimExamStageId;
            var ministrySimExamId = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.Id == stageId)
                .Select(s => s.MinistrySimExamId)
                .FirstOrDefaultAsync();

            _context.MinistrySimExamStageQuestions.Remove(link);
            await _context.SaveChangesAsync();

            await RenumberStageOrderAsync(stageId);
            await RecalculateGlobalOrderAsync(ministrySimExamId);
        }

        // Sprint 7 (MSE-D / D3): يستبدل كل أسئلة مؤشر واحد (Lesson+Difficulty) ضمن مرحلة باختيار عشوائي جديد بنفس
        // RequestedCount المسجَّل أصلاً لهذا المؤشر — استدعاء ضيّق النطاق لنفس منطق SelectAxisQuestionsAsync، بلا أي
        // تأثير على بقية مؤشرات المرحلة (القرار #2: بدون تكملة صامتة، أي نقص يُسجَّل ويمنع النشر لاحقًا).
        public async Task<MinistrySimExamGenerationResult> RegenerateIndicatorAsync(int stageId, int lessonId, DifficultyLevel difficulty)
        {
            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stageId);

            if (stage == null)
                throw new InvalidOperationException("لم يتم العثور على قسم اختبار معمل القياس المطلوب.");

            var selection = await _context.MinistrySimExamStageIndicatorSelections
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MinistrySimExamStageId == stageId && x.LessonId == lessonId && x.Difficulty == difficulty);

            if (selection == null)
                throw new InvalidOperationException("لم يتم العثور على اختيار هذا المؤشر بهذه الصعوبة ضمن هذا القسم.");

            var lessonTitle = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync() ?? $"مؤشر #{lessonId}";

            var isQuant = selection.IsQuant;
            var sectionTitle = await _context.Sections
                .AsNoTracking()
                .Where(s => s.Id == selection.SectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync() ?? (isQuant ? "المحور الكمي" : "المحور اللفظي");

            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == stageId)
                .ToListAsync();

            var stageQuestionIds = stageQuestions.Select(q => q.QuestionId).ToList();

            // EF.Constant() يجبر الترجمة على IN (...) حرفية بدل OPENJSON غير المدعوم على SQL Server 2014
            var questionMeta = await _context.Questions
                .AsNoTracking()
                .Where(q => EF.Constant(stageQuestionIds).Contains(q.Id))
                .Select(q => new { q.Id, q.LessonId, q.Difficulty })
                .ToListAsync();

            var toRemove = stageQuestions
                .Where(sq => questionMeta.Any(m => m.Id == sq.QuestionId && m.LessonId == lessonId && m.Difficulty == difficulty))
                .ToList();

            // منع التكرار على مستوى الاختبار كله، لا هذا القسم فقط: كل ما يبقى بعد إزالة أسئلة هذا المؤشر يدخل في المقارنة
            var removedLinkIds = toRemove.Select(sq => sq.Id).ToHashSet();
            var selectedInExam = (await LoadExamQuestionTextsAsync(stage.MinistrySimExamId))
                .Where(q => !removedLinkIds.Contains(q.StageQuestionId))
                .ToList();

            var usedElsewhere = selectedInExam
                .Select(q => q.Id)
                .Distinct()
                .ToList();

            var similarityThreshold = await GetSimilarityThresholdAsync();

            if (toRemove.Any())
                _context.MinistrySimExamStageQuestions.RemoveRange(toRemove);
            await _context.SaveChangesAsync();

            // نفس شروط جودة السؤال المستخدمة في ExamQuestionSelectorService — بدون أي تكملة تلقائية للنقص من مصدر آخر
            var candidates = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId
                            && q.Difficulty == difficulty
                            && q.IsReviewed && !q.IsRejected && q.CorrectAnswer != null
                            && !EF.Constant(usedElsewhere).Contains(q.Id))
                .Select(q => new MinistrySimExamQuestionText
                {
                    Id = q.Id,
                    LessonId = q.LessonId,
                    Difficulty = q.Difficulty,
                    VerbalPassageId = q.VerbalPassageId,
                    ImageUrl = q.ImageUrl,
                    Title = q.Title,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    CorrectAnswer = q.CorrectAnswer
                })
                .ToListAsync();

            var rnd = new Random();
            var (pickedQuestions, rejectedAsSimilar) = PickDistinctQuestions(
                candidates.OrderBy(_ => rnd.Next()).ToList(), selection.RequestedCount, selectedInExam, similarityThreshold);
            var picked = pickedQuestions.Select(q => q.Id).ToList();

            var currentMaxOrder = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == stageId)
                .Select(q => (int?)q.StageOrder)
                .MaxAsync() ?? 0;

            var newLinks = picked.Select((qId, idx) => new MinistrySimExamStageQuestion
            {
                MinistrySimExamStageId = stageId,
                QuestionId = qId,
                IsQuant = isQuant,
                IsManuallySelected = false,
                StageOrder = currentMaxOrder + idx + 1
            }).ToList();

            if (newLinks.Any())
                await _context.BulkInsertAsync(newLinks);

            await RenumberStageOrderAsync(stageId);
            await RecalculateGlobalOrderAsync(stage.MinistrySimExamId);

            var result = new MinistrySimExamGenerationResult { IsSuccess = picked.Count == selection.RequestedCount };

            if (picked.Count < selection.RequestedCount)
            {
                var shortfall = selection.RequestedCount - picked.Count;
                result.ShortfallMessages.Add(
                    $"{QdratNew.Helpers.SectionNameHelper.Name(stage.StageNumber)} - المحور «{sectionTitle}» - المؤشر «{lessonTitle}» - صعوبة {DifficultyArabic(difficulty)}: " +
                    $"مطلوب {selection.RequestedCount} سؤال وناقص {shortfall} (المتاح فعليًا في البنك {picked.Count}{SimilarityNote(rejectedAsSimilar)}).");
            }

            return result;
        }

        // Sprint 7 (MSE-D / D4): يضيف سؤالاً واحدًا محددًا يدويًا إلى المرحلة، بعد التحقق أنه من نفس المؤشر ونفس مستوى
        // الصعوبة تحديدًا (منع إفساد بنية المؤشرات) وغير مستخدم بالفعل داخل نفس المرحلة.
        public async Task<MinistrySimExamManualAddResult> AddQuestionManuallyAsync(int stageId, int lessonId, DifficultyLevel difficulty, Guid questionId)
        {
            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stageId);

            if (stage == null)
                return new MinistrySimExamManualAddResult { Success = false, Message = "لم يتم العثور على قسم اختبار معمل القياس المطلوب." };

            var question = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return new MinistrySimExamManualAddResult { Success = false, Message = "السؤال المطلوب إضافته غير موجود." };

            if (question.LessonId != lessonId || question.Difficulty != difficulty)
                return new MinistrySimExamManualAddResult { Success = false, Message = "السؤال المختار يجب أن يكون من نفس المؤشر ونفس مستوى الصعوبة المطلوبين." };

            var alreadyUsed = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .AnyAsync(q => q.MinistrySimExamStageId == stageId && q.QuestionId == questionId);

            if (alreadyUsed)
                return new MinistrySimExamManualAddResult { Success = false, Message = "هذا السؤال مستخدم بالفعل داخل نفس القسم." };

            var conflict = await FindDuplicateConflictAsync(stage.MinistrySimExamId, questionId);
            if (conflict != null)
                return new MinistrySimExamManualAddResult { Success = false, Message = conflict };

            var selection = await _context.MinistrySimExamStageIndicatorSelections
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MinistrySimExamStageId == stageId && x.LessonId == lessonId && x.Difficulty == difficulty);

            if (selection == null)
                return new MinistrySimExamManualAddResult { Success = false, Message = "لا يوجد اختيار مسجَّل لهذا المؤشر بهذه الصعوبة ضمن هذا القسم." };

            var isQuant = selection.IsQuant;

            var currentMaxOrder = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == stageId)
                .Select(q => (int?)q.StageOrder)
                .MaxAsync() ?? 0;

            _context.MinistrySimExamStageQuestions.Add(new MinistrySimExamStageQuestion
            {
                MinistrySimExamStageId = stageId,
                QuestionId = questionId,
                IsQuant = isQuant,
                IsManuallySelected = true,
                StageOrder = currentMaxOrder + 1
            });
            await _context.SaveChangesAsync();

            await RenumberStageOrderAsync(stageId);
            await RecalculateGlobalOrderAsync(stage.MinistrySimExamId);

            return new MinistrySimExamManualAddResult { Success = true, Message = "تمت إضافة السؤال بنجاح." };
        }

        // Sprint 7 (MSE-D): يعيد ترقيم StageOrder تسلسليًا 1..N بعد أي استبعاد/إضافة/إعادة توليد جزئي داخل مرحلة،
        // محافظًا على الترتيب النسبي للأسئلة غير المتأثرة، تمهيدًا لإعادة حساب GlobalOrder التراكمي للاختبار كاملاً.
        private async Task RenumberStageOrderAsync(int stageId)
        {
            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == stageId)
                .OrderBy(q => q.StageOrder)
                .ThenBy(q => q.Id)
                .ToListAsync();

            var updated = new List<MinistrySimExamStageQuestion>();
            for (int i = 0; i < stageQuestions.Count; i++)
            {
                var newOrder = i + 1;
                if (stageQuestions[i].StageOrder != newOrder)
                {
                    stageQuestions[i].StageOrder = newOrder;
                    updated.Add(stageQuestions[i]);
                }
            }

            if (updated.Any())
                await _context.BulkUpdateAsync(updated);
        }
    }
}
