using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Helpers;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;

namespace QdratNew.Services.Exams.Generators
{
    public class CurriculumExamGeneratorService : ICurriculumExamGeneratorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IExamQuestionSelectorService _questionSelectorService;
        private readonly INotificationCenterService _notificationService;
        private readonly ITimeZoneService _timeZoneService;

        public CurriculumExamGeneratorService(

    IDbContextFactory<ApplicationDbContext> contextFactory,
            ISystemSettingService systemSettingService,
            IExamQuestionSelectorService questionSelectorService,
            INotificationCenterService notificationService,
            ITimeZoneService timeZoneService    )
        {
            _contextFactory = contextFactory;
            _systemSettingService = systemSettingService;
            _questionSelectorService = questionSelectorService;
            _notificationService = notificationService;
            _timeZoneService = timeZoneService;
        }

        // ✅ توليد اختبار على منهج + دفعة + محور
        public async Task<int> GenerateExamForCurriculumAsync(
            int curriculumId,
            int batchId,
            int sectionId,
            int? createdByInstructorId = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var existingAssignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a =>
                    a.BatchId == batchId &&
                    a.CurriculumId == curriculumId &&
                    a.SectionId == sectionId);

            if (existingAssignment != null)
                return existingAssignment.Id;

            var curriculum = await _context.Curriculums.FindAsync(curriculumId);
            var section = await _context.Sections.FindAsync(sectionId);
            var batch = await _context.Batches.FindAsync(batchId);

            if (curriculum == null || section == null || batch == null)
                throw new Exception("❌ البيانات غير صحيحة لتوليد الاختبار");

            // 🟢 لو مش متحدد عدد → fallback على الإعداد
            var totalQuestions = await _systemSettingService.GetIntAsync("CurriculumExamQuestionsCount", 40);

            int easy = totalQuestions / 3;
            int medium = totalQuestions / 3;
            int hard = totalQuestions - easy - medium;

            var exam = new Exam
            {
                Title = $"اختبار على المحور: {section.Title}",
                Type = ExamType.PerformanceScale,
                CurriculumId = curriculumId,
                SectionId = sectionId,
                TotalQuestions = totalQuestions,
                EasyQuestionCount = easy,
                MediumQuestionCount = medium,
                HardQuestionCount = hard,
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = _timeZoneService.GetNowUtc()
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                CurriculumId = curriculumId,
                SectionId = sectionId,
                BatchId = batchId,
                Title = exam.Title,
                TotalQuestions = totalQuestions,
                EasyQuestionCount = easy,
                MediumQuestionCount = medium,
                HardQuestionCount = hard,
                DurationMinutes = 30,
                CreatedAt = _timeZoneService.GetNowUtc(),
                IsOnline = true,
                IsInLab = false,
                RequireAttendanceBeforeExam = false,
                CreatedByInstructorId = createdByInstructorId
            };
            _context.ExamAssignmentsToBatches.Add(assignment);
            await _context.SaveChangesAsync();

            var questions = await _questionSelectorService.SelectQuestionsAsync(
                curriculumId,
                totalQuestions,
                easy,
                medium,
                hard,
                QuestionUsageType.PerformanceScale);

            int order = 1;
            foreach (var q in questions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.Id,
                    QuestionId = q.Id,
                    Order = order++,
                    ExamAssignmentId = assignment.Id
                });
            }
            await _context.SaveChangesAsync();

            return assignment.Id;
        }

        // ✅ توليد اختبار على منهج + دفعة فقط (بدون محور) مع دعم العدد من الواجهة أو الإعدادات
        public async Task<int> GenerateExamForCurriculumWithQuestionsAsync(
         int curriculumId,
         int batchId,
         int questionCount,
         int? createdByInstructorId = null,
         ExamType examType = ExamType.Course)
        {
            using var _context = _contextFactory.CreateDbContext();

            // التشيك على وجود Assignment قديم
            var existingAssignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a =>
                    a.BatchId == batchId &&
                    a.CurriculumId == curriculumId &&
                    a.SectionId == null);

            if (existingAssignment != null)
            {
                // ✅ تحديث العدد في الجدول الجديد
                var existingCount = await _context.ExamCurriculumQuestionCounts
                    .FirstOrDefaultAsync(x => x.ExamAssignmentId == existingAssignment.Id && x.CurriculumId == curriculumId);

                if (existingCount != null)
                {
                    existingCount.QuestionCount = questionCount;
                }
                else
                {
                    _context.ExamCurriculumQuestionCounts.Add(new ExamCurriculumQuestionCount
                    {
                        ExamAssignmentId = existingAssignment.Id,
                        CurriculumId = curriculumId,
                        QuestionCount = questionCount
                    });
                }

                await _context.SaveChangesAsync();
                return existingAssignment.Id;
            }

            // إنشاء Exam جديد
            var exam = new Exam
            {
                Title = $"اختبار على المنهج: {_context.Curriculums.Find(curriculumId)?.Title}",
                Type = examType,
                CurriculumId = curriculumId,
                TotalQuestions = questionCount,
                DurationMinutes = 60,
                IsActive = true,
                CreatedAt = _timeZoneService.GetNowUtc()
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            // Assignment
            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                CurriculumId = curriculumId,
                BatchId = batchId,
                Title = exam.Title,
                TotalQuestions = questionCount,
                DurationMinutes = 60,
                CreatedAt = _timeZoneService.GetNowUtc(),
                IsOnline = true
            };
            _context.ExamAssignmentsToBatches.Add(assignment);
            await _context.SaveChangesAsync();

            // ✅ حفظ توزيع الأسئلة في جدول جديد
            _context.ExamCurriculumQuestionCounts.Add(new ExamCurriculumQuestionCount
            {
                ExamAssignmentId = assignment.Id,
                CurriculumId = curriculumId,
                QuestionCount = questionCount
            });
            await _context.SaveChangesAsync();

            // توليد الأسئلة (هنا تعتمد على questionCount مش SystemSettings)
            var easy = questionCount / 3;
            var medium = questionCount / 3;
            var hard = questionCount - easy - medium;

            var questions = await _questionSelectorService.SelectQuestionsAsync(
                curriculumId,
                questionCount,
                easy,
                medium,
                hard,
                examType == ExamType.Course ? QuestionUsageType.QdratExam : QuestionUsageType.OfficialMockExam);

            int order = 1;
            foreach (var q in questions.DistinctBy(x => x.Id))
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.Id,
                    QuestionId = q.Id,
                    Order = order++,
                    ExamAssignmentId = assignment.Id
                });
            }

            await _context.SaveChangesAsync();
            return assignment.Id;
        }

        public async Task<int> GenerateMergedExamForCurriculumsAsync(
    int batchId,
    List<(int CurriculumId, int QuestionCount)> curriculumData,
    int? createdByInstructorId = null,
    ExamType examType = ExamType.Course)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (curriculumData == null || curriculumData.Count == 0)
                throw new Exception("❌ لم يتم اختيار أي منهج لتوليد الاختبار");

            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null)
                throw new Exception("❌ لم يتم العثور على الدفعة");

            // اجمالي عدد الاسئلة
            var totalQuestions = curriculumData.Sum(x => x.QuestionCount);

            // إنشاء Exam رئيسي واحد
            var exam = new Exam
            {
                Title = $"اختبار مجمّع للدفعة: {batch.Name}",
                Type = examType,
                TotalQuestions = totalQuestions,
                DurationMinutes = 60,
                IsActive = true,
                CreatedAt = _timeZoneService.GetNowUtc()
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            // Assignment رئيسي
            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                BatchId = batchId,
                Title = exam.Title,
                TotalQuestions = totalQuestions,
                DurationMinutes = 60,
                CreatedAt = _timeZoneService.GetNowUtc(),
                IsOnline = true,
                CreatedByInstructorId = createdByInstructorId
            };
            _context.ExamAssignmentsToBatches.Add(assignment);
            await _context.SaveChangesAsync();

            int order = 1;

            // اختيار الأسئلة لكل منهج
            foreach (var item in curriculumData)
            {
                var easy = item.QuestionCount / 3;
                var medium = item.QuestionCount / 3;
                var hard = item.QuestionCount - easy - medium;

                var questions = await _questionSelectorService.SelectQuestionsAsync(
                    item.CurriculumId,
                    item.QuestionCount,
                    easy,
                    medium,
                    hard,
                    examType == ExamType.Course ? QuestionUsageType.QdratExam : QuestionUsageType.OfficialMockExam);

                foreach (var q in questions.DistinctBy(x => x.Id))
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = exam.Id,
                        QuestionId = q.Id,
                        Order = order++,
                        ExamAssignmentId = assignment.Id
                    });
                }

                // حفظ توزيع الأسئلة لكل منهج
                _context.ExamCurriculumQuestionCounts.Add(new ExamCurriculumQuestionCount
                {
                    ExamAssignmentId = assignment.Id,
                    CurriculumId = item.CurriculumId,
                    QuestionCount = item.QuestionCount
                });
            }

            await _context.SaveChangesAsync();
            return assignment.Id;
        }




        public async Task<int> AssignExamToSpecificStudentsAsync(int examAssignmentId, List<int> studentIds)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (studentIds == null || !studentIds.Any())
                return 0;

            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(x => x.Id == examAssignmentId);

            if (assignment == null)
                throw new Exception("❌ لم يتم العثور على الاختبار المحدد.");

            var now = _timeZoneService.GetNowUtc();

            // 🟢 إنشاء حالة اختبار فقط للطلاب المحددين
            foreach (var sid in studentIds)
            {
                bool exists = await _context.ExamStudentStatuses
                    .AnyAsync(s => s.ExamAssignmentId == examAssignmentId && s.StudentId == sid);

                if (!exists)
                {
                    _context.ExamStudentStatuses.Add(new ExamStudentStatus
                    {
                        ExamId = assignment.ExamId.GetValueOrDefault(),
                        ExamAssignmentId = assignment.Id,
                        StudentId = sid,
                        Status = ExamStatus.Pending,
                        AssignedAt = now,
                        IsSubmitted = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return studentIds.Count;
        }


    }
}
