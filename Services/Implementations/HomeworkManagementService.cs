using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Models;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class HomeworkManagementService : IHomeworkManagementService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IAdminActivityLogger _activityLogger;
        private readonly ITimeZoneService _timeZoneService;

        public HomeworkManagementService(
             IDbContextFactory<ApplicationDbContext> contextFactory,
            IAdminActivityLogger activityLogger,
            ITimeZoneService timeZoneService)
        {
            _contextFactory = contextFactory;
            _activityLogger = activityLogger;
            _timeZoneService = timeZoneService;
        }

        public async Task<List<HomeworkOverviewViewModel>> GetHomeworksAsync(
          DateTime? fromDate,
          DateTime? toDate,
          int? batchId,
          bool showArchived = false)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ===============================
            // 1) تجهيز اسم الدفعة مرة واحدة
            // ===============================
            string batchName = null;

            if (batchId.HasValue)
            {
                batchName = await _context.Batches
                    .Where(b => b.Id == batchId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync();
            }

            // ===============================
            // 2) جلب HomeworkSets + Batch (بدون Tracking)
            // ===============================
            var baseQuery =
                from hs in _context.HomeworkSets.AsNoTracking()
                join b in _context.Batches.AsNoTracking()
                    on hs.BatchId equals b.Id
                select new
                {
                    HomeworkSetId = hs.Id,
                    hs.CreatedAt,
                    BatchId = hs.BatchId,
                    BatchName = b.Name,
                    hs.Title,
                    hs.CompletionTitle,
                    hs.IsArchived
                };

            baseQuery = baseQuery.Where(x => x.IsArchived == showArchived);

            if (fromDate.HasValue)
                baseQuery = baseQuery.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                baseQuery = baseQuery.Where(x => x.CreatedAt <= toDate.Value);

            if (batchId.HasValue && batchName != null)
                baseQuery = baseQuery.Where(x => x.BatchName == batchName);

            var homeworkSets = await baseQuery
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            if (!homeworkSets.Any())
                return new List<HomeworkOverviewViewModel>();

            // ===============================
            // 3) جلب ALL Homeworks مرة واحدة (بدون أي فلترة IDs)
            // ===============================
            var homeworks = await _context.Homeworks
                .AsNoTracking()
                .Select(h => new
                {
                    h.HomeworkSetId,
                    h.StudentId,
                    h.IsSent,
                    h.Status
                })
                .ToListAsync();

            // ===============================
            // 4) فلترة وتجميع داخل الذاكرة فقط
            // ===============================
            var homeworkSetIdSet = homeworkSets
                .Select(x => x.HomeworkSetId)
                .ToHashSet();

            var groupedHomeworks = homeworks
                .Where(h => homeworkSetIdSet.Contains(h.HomeworkSetId)) // ⬅️ In-Memory فقط
                .GroupBy(h => h.HomeworkSetId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ===============================
            // 5) بناء ViewModel النهائي
            // ===============================
            var result = homeworkSets.Select(hs =>
            {
                groupedHomeworks.TryGetValue(hs.HomeworkSetId, out var hsHomeworks);
                var safeHomeworks = hsHomeworks ?? Enumerable.Empty<dynamic>();

                return new HomeworkOverviewViewModel
                {
                    HomeworkSetId = hs.HomeworkSetId,
                    CreatedAt = hs.CreatedAt,
                    BatchName = hs.BatchName,
                    CompletionTitle = hs.Title,
                    IsArchived = hs.IsArchived,

                    QuestionCount = safeHomeworks.Count(),

                    StudentCount = safeHomeworks
                        .Select(x => x.StudentId)
                        .Distinct()
                        .Count(),

                    HasBeenResent = safeHomeworks.Any(x =>
                        x.IsSent && x.Status == HomeworkStatus.Pending)
                };
            }).ToList();

            return result;
        }


        public async Task<HomeworkDetailsViewModel> GetHomeworkDetailsAsync(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ===============================
            // 1) جلب الواجب + الدفعة
            // ===============================
            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null)
                return null;

            var batchId = homeworkSet.BatchId;
            var now = DateTime.Now;

            // ===============================
            // 2) جلب كل طلاب الدفعة (المصدر الأساسي)
            // ===============================
            var batchStudents = await _context.StudentBatchEnrollments
                .Where(x => x.BatchId == batchId)
                .Select(x => x.Student)
                .AsNoTracking()
                .ToListAsync();

            // ===============================
            // 3) جلب HomeworkSetStudents (من بدأ أو وُلّد له الواجب)
            // ===============================
            var homeworkSetStudents = await _context.HomeworkSetStudents
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();

            // ===============================
            // 4) جلب كل أسئلة الواجب مرة واحدة
            // ===============================
            var allHomeworks = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();


            // ===============================
            // 🔥 4.1) جلب محاولات الأسئلة (Analysis)
            // ===============================
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.HomeworkSetId == homeworkSetId)
                .Select(a => new
                {
                    a.StudentId,
                    a.TimeTakenSeconds,
                    a.IsCorrect
                })
                .ToListAsync();

            // ===============================
            // 🛡️ Translation Guard: طلاب موقوفون حاليًا على هذا الواجب
            // ===============================
            var blockedStudentIds = await _context.IntegrityViolationLogs
                .Where(v => v.AttemptType == IntegrityAttemptType.Homework
                         && v.AttemptEntityId == homeworkSetId
                         && !v.IsResolved)
                .Select(v => v.StudentId)
                .ToListAsync();
            var blockedStudentIdsSet = blockedStudentIds.ToHashSet();



            // ===============================
            // 5) بناء القائمة النهائية
            // ===============================

            var students = batchStudents.Select(student =>
            {
                var hss = homeworkSetStudents
                    .FirstOrDefault(x => x.StudentId == student.StudentID);

                var studentHomeworks = allHomeworks
                    .Where(h => h.StudentId == student.StudentID)
                    .ToList();


                double? score = null;

                if (hss != null)
                {
                    score = hss.Score;
                }

                var assignedAt = hss?.AssignedAt ?? homeworkSet.CreatedAt;
                var hoursSinceAssigned = Math.Max(0, (int)Math.Floor((now - assignedAt).TotalHours));
                var isSubmitted = hss != null && hss.IsSubmitted;
                var isOverdue24Hours = !isSubmitted && hoursSinceAssigned >= 24;
                var isHighRiskLateSubmission = !isSubmitted && hoursSinceAssigned >= 72;
                // ===============================
                // 🔥 تحليل السلوك
                // ===============================
                var studentAttempts = attempts
                    .Where(a => a.StudentId == student.StudentID)
                    .ToList();

                double avgTime = 0;
                int fastAnswers = 0;
                double correctRate = 0;
                double riskScore = 0;

                if (studentAttempts.Any())
                {
                    var total = studentAttempts.Count;

                    avgTime = studentAttempts.Average(x => x.TimeTakenSeconds);
                    fastAnswers = studentAttempts.Count(x => x.TimeTakenSeconds <= 2);
                    correctRate = studentAttempts.Count(x => x.IsCorrect) / (double)total;

                    // =========================
                    // 🧠 Risk Score Calculation
                    // =========================

                    // السرعة
                    if (avgTime < 3)
                        riskScore += 40;
                    else if (avgTime < 5)
                        riskScore += 20;

                    // الإجابات السريعة
                    var fastRatio = fastAnswers / (double)total;
                    if (fastRatio > 0.7)
                        riskScore += 30;
                    else if (fastRatio > 0.4)
                        riskScore += 15;

                    // نسبة الصحة
                    if (correctRate < 0.3)
                        riskScore += 20;
                    else if (correctRate < 0.5)
                        riskScore += 10;

                    riskScore = Math.Min(riskScore, 100);
                }

                // ===============================
                // 🎯 تحويل إلى Enum عربي
                // ===============================
                StudentBehaviorLevel behaviorLevel = StudentBehaviorLevel.طبيعي;

                if (riskScore >= 70)
                    behaviorLevel = StudentBehaviorLevel.غش_محتمل;
                else if (riskScore >= 40)
                    behaviorLevel = StudentBehaviorLevel.مريب;

                return new HomeworkStudentViewModel
                {
                    StudentId = student.StudentID,
                    StudentName = student.FullName,
                    Score = score,
                    HomeworkCount = studentHomeworks.Count,
                    HomeworkIds = studentHomeworks.Select(x => x.Id).ToList(),

                    IsSent = studentHomeworks.Any() && studentHomeworks.All(x => x.IsSent),

                    Status = isSubmitted
                        ? HomeworkStatus.Submitted
                        : HomeworkStatus.Pending,

                    // ===============================
                    // 🔥 الجديد
                    // ===============================
                    RiskScore = Math.Round(riskScore, 1),
                    BehaviorLevel = behaviorLevel,
                    AssignedAt = assignedAt,
                    HoursSinceAssigned = hoursSinceAssigned,
                    IsOverdue24Hours = isOverdue24Hours,
                    IsHighRiskLateSubmission = isHighRiskLateSubmission,
                    StudentReminderContacted = hss != null && hss.StudentReminderContacted,
                    StudentReminderContactedAt = hss?.StudentReminderContactedAt,
                    ParentContacted = hss != null && hss.ParentContacted,
                    ParentContactedAt = hss?.ParentContactedAt,
                    IsIntegrityBlocked = blockedStudentIdsSet.Contains(student.StudentID)
                };

            }).ToList();


            // ===============================
            // 📊 تقرير ولي الأمر
            // ===============================
            var totalStudents = students.Count;
            var submitted = students.Count(x => x.Status == HomeworkStatus.Submitted);
            var cheating = students.Count(x => x.BehaviorLevel == StudentBehaviorLevel.غش_محتمل);
            var suspicious = students.Count(x => x.BehaviorLevel == StudentBehaviorLevel.مريب);

            string report = "";

            if (totalStudents > 0)
            {
                var completionRate = Math.Round((submitted * 100.0) / totalStudents, 1);

                report = $"📘 تم حل الواجب بنسبة {completionRate}% من الطلاب. ";

                if (cheating > 0)
                    report += $"⚠️ يوجد {cheating} طالب يظهر عليه سلوك غش محتمل. ";

                if (suspicious > 0)
                    report += $"🔎 يوجد {suspicious} طالب سلوكه مريب ويحتاج متابعة. ";

                if (cheating == 0 && suspicious == 0)
                    report += $"✅ سلوك الطلاب طبيعي أثناء الحل.";
            }



            // ===============================
            // 6) الإرجاع النهائي
            // ===============================
            return new HomeworkDetailsViewModel
            {
                HomeworkSetId = homeworkSet.Id,
                CreatedAt = homeworkSet.CreatedAt,
                BatchId = batchId,
                BatchName = homeworkSet.Batch?.Name ?? $"دفعة رقم {batchId}",
                Students = students,
                ParentReportSummary = report
            };
        }

        public async Task<bool> ResendHomeworkToBatchAsync(int homeworkSetId, int assignedByUserId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null)
                return false;

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == homeworkSet.BatchId)
                .ToListAsync();

            var lessonQuestionPairs = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId)
                .Select(h => new { h.LessonId, h.QuestionId, h.LectureId })
                .Distinct()
                .ToListAsync();

            var nowUtc = _timeZoneService.GetNowUtc();

            foreach (var student in students)
            {
                bool alreadyAssigned = await _context.Homeworks
                    .AnyAsync(h => h.HomeworkSetId == homeworkSetId && h.StudentId == student.StudentID);

                if (alreadyAssigned)
                    continue;

                foreach (var pair in lessonQuestionPairs)
                {
                    _context.Homeworks.Add(new QdratNew.Entities.Homework
                    {
                        StudentId = student.StudentID,
                        QuestionId = pair.QuestionId,
                        LessonId = pair.LessonId,
                        LectureId = pair.LectureId,
                        HomeworkSetId = homeworkSetId,
                        AssignedAt = nowUtc, // ✅ UTC ثابت
                        IsSent = true,
                        Status = HomeworkStatus.Pending
                    });
                }
            }

            homeworkSet.IsSent = true;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int?> GetHomeworkSetIdFromHomework(int homeworkId)
        {
            using var _context = _contextFactory.CreateDbContext();

            return await _context.Homeworks
                .Where(h => h.Id == homeworkId)
                .Select(h => (int?)h.HomeworkSetId)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ResendHomeworkToStudentAsync(int homeworkId, int adminUserId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var homework = await _context.Homeworks
                .Include(h => h.HomeworkSet)
                .FirstOrDefaultAsync(h => h.Id == homeworkId);

            if (homework == null)
                return false;

            var homeworkSet = homework.HomeworkSet;
            if (homeworkSet == null)
                return false;

            // 🔹 1) التحقق من السماح بالإعادة
            if (!homeworkSet.AllowRetake)
                return false;

            var studentId = homework.StudentId;

            // 🔹 2) حساب عدد المحاولات السابقة
            var attemptsCount = await _context.HomeworkSetAttempts
                .Where(a => a.HomeworkSetId == homeworkSet.Id && a.StudentId == studentId)
                .CountAsync();

            if (attemptsCount >= homeworkSet.MaxRetakes)
                return false;

            var nowUtc = _timeZoneService.GetNowUtc();

            // 🔹 3) إنشاء Attempt جديد
            var newAttempt = new HomeworkSetAttempt
            {
                HomeworkSetId = homeworkSet.Id,
                StudentId = studentId,
                AttemptNumber = attemptsCount + 1,
                StartedAt = nowUtc
            };

            _context.HomeworkSetAttempts.Add(newAttempt);

            // 🔹 4) تصفير جميع أسئلة الطالب داخل هذا الواجب
            var studentHomeworks = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSet.Id && h.StudentId == studentId)
                .ToListAsync();

            foreach (var hw in studentHomeworks)
            {
                hw.IsCompleted = false;
                hw.StudentAnswer = null;
                hw.IsCorrect = null;
                hw.SubmittedAt = null;
                hw.Score = null;
                hw.Status = HomeworkStatus.Pending;
                hw.StartTime = null;
                hw.AnsweredAt = null;
                hw.TimeSpentSeconds = null;
                hw.IsSent = true;
                hw.AssignedAt = nowUtc;
            }

            // 🔹 5) تصفير حالة الواجب في HomeworkSetStudent
            var setStudent = await _context.HomeworkSetStudents
                .FirstOrDefaultAsync(s =>
                    s.HomeworkSetId == homeworkSet.Id &&
                    s.StudentId == studentId);

            if (setStudent != null)
            {
                setStudent.IsSubmitted = false;
                setStudent.SubmittedAt = null;
                setStudent.Score = null;
                setStudent.LastUpdated = nowUtc;
            }

            // 🔹 6) حذف محاولات الأسئلة القديمة الخاصة بهذا الواجب فقط
            var oldAttempts = await _context.QuestionAttemptNew
                .Where(a =>
                    a.HomeworkSetId == homeworkSet.Id &&
                    a.StudentId == studentId)
                .ToListAsync();

            if (oldAttempts.Any())
                _context.QuestionAttemptNew.RemoveRange(oldAttempts);

            await _context.SaveChangesAsync();

            await _activityLogger.LogAsync(
                "إعادة محاولة واجب",
                $"تم إنشاء محاولة جديدة للطالب ID: {studentId} - HomeworkSet: {homeworkSet.Id}",
                null,
                null,
                adminUserId,
                studentId,
                homework.Id
            );

            return true;
        }
    }
}
