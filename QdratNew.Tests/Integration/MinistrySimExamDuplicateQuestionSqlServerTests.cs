using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Implementations;
using QdratNew.Services.Exams.Models;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // قواعد التشابه نفسها، بلا قاعدة بيانات
    public sealed class MinistrySimExamQuestionSimilarityTests
    {
        private static MinistrySimExamQuestionText Q(string title, int? passage = null, string? image = null, string? answer = null) => new()
        {
            Id = Guid.NewGuid(), Title = title, VerbalPassageId = passage, ImageUrl = image!, CorrectAnswer = answer!
        };

        [Fact]
        public void SameQuestionId_IsDuplicate_EvenWithSimilarityOff()
        {
            var q = Q("سؤال");
            Assert.True(MinistrySimExamQuestionSimilarity.IsDuplicate(q, q, thresholdPercent: 0));
        }

        [Fact]
        public void CopyWithSmallDifferences_IsDuplicateAt70()
        {
            var left = Q("أسهم .......... العرب والمسلمون فى النهوض بالحضارة ...............", answer: "العلماء - الانسانية");
            var right = Q("ساعد .......... العرب والمسلمون بالنهوض بالحضارة ........", answer: "العلماء ـ الإنسانية");

            Assert.True(MinistrySimExamQuestionSimilarity.IsDuplicate(left, right, 70));
        }

        [Fact]
        public void PersianLettersAndDigits_CountAsSameText()
        {
            var left = Q("شبکه : صيد سلسلة : قيد ۸۰۰۰");
            var right = Q("شبكة : صيد سلسلة : قيد ٨٠٠٠");

            Assert.True(MinistrySimExamQuestionSimilarity.ScorePercent(left, right) >= 95);
        }

        [Fact]
        public void UnrelatedQuestions_AreNotDuplicate()
        {
            var left = Q("اشترى تاجر بضاعة بمبلغ معين ثم باعها بربح، فما نسبة الربح من التكلفة؟");
            var right = Q("مرادف كلمة الشجاعة في الجملة التالية هو أحد الخيارات المذكورة");

            Assert.False(MinistrySimExamQuestionSimilarity.IsDuplicate(left, right, 70));
        }

        [Fact]
        public void SameTitle_OnDifferentPassages_IsNotDuplicate()
        {
            var title = "ما الفكرة الرئيسية للنص السابق؟";
            Assert.False(MinistrySimExamQuestionSimilarity.IsDuplicate(Q(title, passage: 1), Q(title, passage: 2), 70));
        }

        [Fact]
        public void SameTitle_OnSamePassage_IsDuplicate()
        {
            var title = "ما الفكرة الرئيسية للنص السابق؟";
            Assert.True(MinistrySimExamQuestionSimilarity.IsDuplicate(Q(title, passage: 1), Q(title, passage: 1), 70));
        }

        [Fact]
        public void SameTitle_OnDifferentImages_IsNotDuplicate()
        {
            var title = "أوجد قيمة س في الشكل المقابل";
            Assert.False(MinistrySimExamQuestionSimilarity.IsDuplicate(Q(title, image: "/a.png"), Q(title, image: "/b.png"), 70));
        }

        [Fact]
        public void NoText_SameImage_IsDuplicate_DifferentImage_IsNot()
        {
            Assert.True(MinistrySimExamQuestionSimilarity.IsDuplicate(Q("", image: "/a.png"), Q("", image: "/a.png"), 70));
            Assert.False(MinistrySimExamQuestionSimilarity.IsDuplicate(Q("", image: "/a.png"), Q("", image: "/b.png"), 70));
        }

        [Theory]
        [InlineData("80", 80)]
        [InlineData("0", 0)]
        [InlineData(null, 70)]
        [InlineData("", 70)]
        [InlineData("abc", 70)]
        [InlineData("150", 70)]
        [InlineData("-5", 70)]
        public void ParseThreshold_FallsBackToDefault_OnInvalidValue(string? value, int expected)
        {
            Assert.Equal(expected, MinistrySimExamQuestionSimilarity.ParseThreshold(value!));
        }
    }

    // المولّد وبوابة النشر على قاعدة الاختبار الحقيقية: منع التكرار على مستوى الاختبار كله
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class MinistrySimExamDuplicateQuestionSqlServerTests : IAsyncLifetime
    {
        private const string Marker = "MSEDUP";
        private const string ExamTitle = Marker + " اختبار منع التكرار";
        private const string LessonTitle = Marker + " مؤشر";
        private const DifficultyLevel Level = DifficultyLevel.Medium;

        private int _examId;
        private int _stage1Id;
        private int _stage2Id;
        private int _lessonId;
        private int _sectionId;
        private int _curriculumId;

        // نصوص متباعدة عمدًا، وNearCopy نسخة شبه مطابقة من Distinct[0]
        private static readonly string[] Distinct =
        {
            "اشترى تاجر بضاعة بمبلغ معين من المال ثم باعها بربح فما نسبة ربحه من التكلفة الكلية",
            "مرادف كلمة الشجاعة في الجملة الواردة أعلاه هو أحد البدائل الأربعة المذكورة أدناه",
            "إذا تحرك قطار بسرعة ثابتة مسافة طويلة خلال ساعات محددة فكم يقطع في نصف الوقت",
            "العلاقة بين الكتاب والمكتبة تشبه العلاقة بين الطبيب وأحد الأماكن التالية في المدينة"
        };

        private const string NearCopy = "اشترى تاجر بضاعه بمبلغ معين من المال ثم باعها بربح فما نسبة ربحه من التكلفة الكليه كلها";

        private readonly Dictionary<string, Guid> _ids = new();

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupAsync();

            await using var db = RtkRealDb.CreateDb();
            var template = await db.Lessons
                .Where(l => l.Section != null)
                .Select(l => new { l.UnitId, l.SectionId, l.Section.CurriculumId })
                .FirstAsync();
            _sectionId = template.SectionId;
            _curriculumId = template.CurriculumId;

            var lesson = new Lesson { Title = LessonTitle, Content = Marker, UnitId = template.UnitId, SectionId = template.SectionId };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();
            _lessonId = lesson.Id;

            var exam = new MinistrySimExam
            {
                CourseId = await db.Courses.Select(c => c.Id).FirstAsync(),
                Title = ExamTitle,
                TotalStages = 2,
                CreatedByInstructorId = await db.Instructors.Select(i => i.Id).FirstAsync()
            };
            var stage1 = NewStage(1);
            var stage2 = NewStage(2);
            exam.Stages.Add(stage1);
            exam.Stages.Add(stage2);
            db.MinistrySimExams.Add(exam);
            await db.SaveChangesAsync();

            _examId = exam.Id;
            _stage1Id = stage1.Id;
            _stage2Id = stage2.Id;
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await CleanupAsync();
        }

        private MinistrySimExamStage NewStage(int number) => new()
        {
            StageNumber = number, QuantSectionId = _sectionId, QuantQuestionCount = 0,
            VerbalSectionId = _sectionId, VerbalQuestionCount = 0, DurationMinutes = 26
        };

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            await db.SystemSettings.Where(s => s.Key == MinistrySimExamQuestionSimilarity.ThresholdSettingKey).ExecuteDeleteAsync();

            var examIds = await db.MinistrySimExams.Where(e => e.Title == ExamTitle).Select(e => e.Id).ToListAsync();
            foreach (var id in examIds)
            {
                await db.MinistrySimExamStageQuestions.Where(q => q.MinistrySimExamStage.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStageIndicatorSelections.Where(s => s.MinistrySimExamStage.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStages.Where(s => s.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExams.Where(e => e.Id == id).ExecuteDeleteAsync();
            }

            await db.Questions.Where(q => q.Lesson.Title == LessonTitle).ExecuteDeleteAsync();
            await db.Lessons.Where(l => l.Title == LessonTitle).ExecuteDeleteAsync();
        }

        private async Task SeedBankAsync(params string[] titles)
        {
            await using var db = RtkRealDb.CreateDb();
            var questions = titles.Select(title => new Question
            {
                Title = title,
                CorrectAnswer = "أ",
                ReferenceNumber = Guid.NewGuid().ToString("N")[..12],
                CurriculumId = _curriculumId,
                LessonId = _lessonId,
                Difficulty = Level,
                IsReviewed = true,
                IsRejected = false
            }).ToList();
            db.Questions.AddRange(questions);
            await db.SaveChangesAsync();

            foreach (var question in questions)
                _ids[question.Title!] = question.Id;
        }

        private List<IndicatorSelectionInput> Ask(int count) => new()
        {
            new IndicatorSelectionInput { SectionId = _sectionId, LessonId = _lessonId, Difficulty = Level, RequestedCount = count }
        };

        private static async Task<MinistrySimExamGenerationResult> GenerateAsync(int stageId, List<IndicatorSelectionInput> selections)
        {
            await using var db = RtkRealDb.CreateDb();
            return await new MinistrySimExamGeneratorService(db)
                .GenerateStageQuestionsAsync(stageId, selections, new List<IndicatorSelectionInput>());
        }

        private async Task<List<Guid>> ExamQuestionIdsAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            return await db.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStage.MinistrySimExamId == _examId)
                .Select(q => q.QuestionId)
                .ToListAsync();
        }

        private async Task LinkDirectlyAsync(int stageId, int order, Guid questionId)
        {
            await using var db = RtkRealDb.CreateDb();
            db.MinistrySimExamStageQuestions.Add(new MinistrySimExamStageQuestion
            {
                MinistrySimExamStageId = stageId, QuestionId = questionId, StageOrder = order, GlobalOrder = order, IsQuant = true
            });
            await db.SaveChangesAsync();
        }

        private static async Task SetThresholdAsync(string value)
        {
            await using var db = RtkRealDb.CreateDb();
            db.SystemSettings.Add(new SystemSetting { Key = MinistrySimExamQuestionSimilarity.ThresholdSettingKey, Value = value });
            await db.SaveChangesAsync();
        }

        [SqlServerFact]
        public async Task Generate_NeverRepeatsSameQuestionAcrossStages()
        {
            await SeedBankAsync(Distinct);

            Assert.True((await GenerateAsync(_stage1Id, Ask(2))).IsSuccess);
            var second = await GenerateAsync(_stage2Id, Ask(3));

            var ids = await ExamQuestionIdsAsync();
            Assert.Equal(4, ids.Count);
            Assert.Equal(4, ids.Distinct().Count());

            // البنك فيه 4 أسئلة فقط: الثالث في القسم الثاني نقص صريح لا تكرار
            Assert.False(second.IsSuccess);
            Assert.Contains(second.ShortfallMessages, m => m.Contains("وناقص 1"));
        }

        [SqlServerFact]
        public async Task Generate_NeverPicksBothOfSimilarPair_AndReportsShortfall()
        {
            await SeedBankAsync(Distinct[0], NearCopy, Distinct[1]);

            var result = await GenerateAsync(_stage1Id, Ask(3));

            var ids = await ExamQuestionIdsAsync();
            Assert.Equal(2, ids.Count);
            Assert.False(ids.Contains(_ids[Distinct[0]]) && ids.Contains(_ids[NearCopy]));
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ShortfallMessages, m => m.Contains("مكرر أو مشابه"));
        }

        [SqlServerFact]
        public async Task Generate_ChecksSimilarityAgainstOtherStages()
        {
            await SeedBankAsync(Distinct[0], NearCopy, Distinct[1]);
            await LinkDirectlyAsync(_stage1Id, 1, _ids[Distinct[0]]);

            await GenerateAsync(_stage2Id, Ask(2));

            var ids = await ExamQuestionIdsAsync();
            Assert.DoesNotContain(_ids[NearCopy], ids);
            Assert.Contains(_ids[Distinct[1]], ids);
        }

        [SqlServerFact]
        public async Task Generate_SimilarityOff_AllowsSimilar_ButStillNotSameQuestion()
        {
            await SetThresholdAsync("0");
            await SeedBankAsync(Distinct[0], NearCopy);

            var result = await GenerateAsync(_stage1Id, Ask(2));

            Assert.True(result.IsSuccess);
            var ids = await ExamQuestionIdsAsync();
            Assert.Equal(2, ids.Distinct().Count());
        }

        [SqlServerFact]
        public async Task Regenerate_ChecksWholeExam()
        {
            // القسم الثاني يحوي Distinct[1] نفسه وDistinct[0]؛ فلا يبقى لإعادة توليد القسم الأول إلا Distinct[2]
            await SeedBankAsync(Distinct[0], NearCopy, Distinct[1], Distinct[2]);
            await GenerateIndicatorRowAsync(_stage1Id, requestedCount: 4);
            await LinkDirectlyAsync(_stage2Id, 1, _ids[Distinct[0]]);
            await LinkDirectlyAsync(_stage2Id, 2, _ids[Distinct[1]]);

            await using var db = RtkRealDb.CreateDb();
            var result = await new MinistrySimExamGeneratorService(db).RegenerateIndicatorAsync(_stage1Id, _lessonId, Level);

            var stage1Ids = await db.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == _stage1Id)
                .Select(q => q.QuestionId)
                .ToListAsync();
            Assert.Equal(new[] { _ids[Distinct[2]] }, stage1Ids);
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ShortfallMessages, m => m.Contains("مكرر أو مشابه"));
        }

        [SqlServerFact]
        public async Task ManualAdd_RejectsSameQuestionInOtherStage_AndSimilarOne()
        {
            await SeedBankAsync(Distinct[0], NearCopy, Distinct[1]);
            await LinkDirectlyAsync(_stage1Id, 1, _ids[Distinct[0]]);
            await GenerateIndicatorRowAsync(_stage2Id);

            await using var db = RtkRealDb.CreateDb();
            var service = new MinistrySimExamGeneratorService(db);

            var same = await service.AddQuestionManuallyAsync(_stage2Id, _lessonId, Level, _ids[Distinct[0]]);
            Assert.False(same.Success);
            Assert.Contains("مستخدم بالفعل", same.Message);

            var similar = await service.AddQuestionManuallyAsync(_stage2Id, _lessonId, Level, _ids[NearCopy]);
            Assert.False(similar.Success);
            Assert.Contains("يشبه", similar.Message);

            var fine = await service.AddQuestionManuallyAsync(_stage2Id, _lessonId, Level, _ids[Distinct[1]]);
            Assert.True(fine.Success);
        }

        private async Task GenerateIndicatorRowAsync(int stageId, int requestedCount = 1)
        {
            await using var db = RtkRealDb.CreateDb();
            db.MinistrySimExamStageIndicatorSelections.Add(new MinistrySimExamStageIndicatorSelection
            {
                MinistrySimExamStageId = stageId, SectionId = _sectionId, LessonId = _lessonId,
                Difficulty = Level, RequestedCount = requestedCount, IsQuant = true
            });
            await db.SaveChangesAsync();
        }

        [SqlServerFact]
        public async Task Replace_IgnoresQuestionBeingReplaced_ButChecksTheRest()
        {
            await SeedBankAsync(Distinct[0], NearCopy, Distinct[1]);
            await LinkDirectlyAsync(_stage1Id, 1, _ids[Distinct[0]]);
            await LinkDirectlyAsync(_stage2Id, 1, _ids[Distinct[1]]);

            await using var db = RtkRealDb.CreateDb();
            var service = new MinistrySimExamGeneratorService(db);
            var linkId = await db.MinistrySimExamStageQuestions
                .Where(q => q.MinistrySimExamStageId == _stage1Id)
                .Select(q => q.Id)
                .SingleAsync();

            // البديل يشبه السؤال الذي سيخرج فقط: مسموح
            Assert.Null(await service.FindDuplicateConflictAsync(_examId, _ids[NearCopy], linkId));

            // بلا استثناء السؤال نفسه: مرفوض
            Assert.NotNull(await service.FindDuplicateConflictAsync(_examId, _ids[NearCopy]));

            // البديل هو سؤال القسم الثاني: مرفوض
            Assert.NotNull(await service.FindDuplicateConflictAsync(_examId, _ids[Distinct[1]], linkId));
        }

        [SqlServerFact]
        public async Task PublishGate_BlocksDuplicates_ButNotAlreadyPublishedExam()
        {
            await SeedBankAsync(Distinct[0], NearCopy);
            await LinkDirectlyAsync(_stage1Id, 1, _ids[Distinct[0]]);
            await LinkDirectlyAsync(_stage2Id, 1, _ids[NearCopy]);
            await LinkDirectlyAsync(_stage2Id, 2, _ids[Distinct[0]]);

            await using var db = RtkRealDb.CreateDb();
            var service = new MinistrySimExamGeneratorService(db);

            var draft = await service.ValidateExactCountsAsync(_examId);
            Assert.False(draft.IsValid);
            Assert.Contains(draft.ShortfallMessages, m => m.Contains("سؤال مكرر"));
            Assert.Contains(draft.ShortfallMessages, m => m.Contains("متشابهان"));

            await db.MinistrySimExams.Where(e => e.Id == _examId).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublished, true));

            var published = await service.ValidateExactCountsAsync(_examId);
            Assert.DoesNotContain(published.ShortfallMessages, m => m.Contains("سؤال مكرر") || m.Contains("متشابهان"));
        }
    }
}
