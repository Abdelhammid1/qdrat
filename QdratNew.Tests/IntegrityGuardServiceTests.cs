using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Implementations;
using Xunit;

namespace QdratNew.Tests
{
    // Sprint 9 (QG-G / G1) — اختبارات وحدة لـ IntegrityGuardService: القفل/التسجيل/إعادة الفتح
    // لمكوّن حماية مسارات الحل من ترجمة المتصفح (Translation Guard).
    public class IntegrityGuardServiceTests
    {
        private static ApplicationDbContext CreateContext()
            => TestDbContextFactory.CreateFreshContext(out _);

        [Fact]
        public async Task IsBlockedAsync_NoViolationLogged_ReturnsFalse()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            var blocked = await service.IsBlockedAsync(1, IntegrityAttemptType.Homework, 100);

            Assert.False(blocked);
        }

        [Fact]
        public async Task IsBlockedAsync_AfterActiveViolation_ReturnsTrue()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.Homework, 100, "BrowserTranslate", "1.2.3.4", "UA", "/page");

            var blocked = await service.IsBlockedAsync(1, IntegrityAttemptType.Homework, 100);

            Assert.True(blocked);
        }

        [Fact]
        public async Task IsBlockedAsync_ViolationLoggedForDifferentAttempt_ReturnsFalse()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.Homework, 100, "BrowserTranslate", null, null, null);

            // نفس الطالب، نوع مختلف لنفس المعرّف — لا يجب أن يُحظر مسار آخر
            var blockedOtherType = await service.IsBlockedAsync(1, IntegrityAttemptType.Exam, 100);
            // نفس الطالب والنوع، معرّف محاولة مختلف — لا يجب أن يُحظر
            var blockedOtherEntity = await service.IsBlockedAsync(1, IntegrityAttemptType.Homework, 200);
            // طالب مختلف تمامًا لنفس المحاولة — لا يجب أن يُحظر (لا تسريب عبر الطلاب)
            var blockedOtherStudent = await service.IsBlockedAsync(2, IntegrityAttemptType.Homework, 100);

            Assert.False(blockedOtherType);
            Assert.False(blockedOtherEntity);
            Assert.False(blockedOtherStudent);
        }

        [Fact]
        public async Task IsBlockedAsync_StudentIdZeroOrNegative_ReturnsFalseWithoutQuery()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            Assert.False(await service.IsBlockedAsync(0, IntegrityAttemptType.Homework, 100));
            Assert.False(await service.IsBlockedAsync(-1, IntegrityAttemptType.Homework, 100));
        }

        [Fact]
        public async Task LogViolationAsync_CalledTwiceForSameActiveAttempt_DoesNotDuplicateRow()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.Exam, 55, "BrowserTranslate", "1.1.1.1", "UA1", "/exam");
            await service.LogViolationAsync(1, IntegrityAttemptType.Exam, 55, "BrowserTranslate", "2.2.2.2", "UA2", "/exam?q=2");

            var count = await db.IntegrityViolationLogs
                .CountAsync(v => v.StudentId == 1 && v.AttemptType == IntegrityAttemptType.Exam && v.AttemptEntityId == 55);

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task LogViolationAsync_StudentIdZeroOrNegative_DoesNotInsertRow()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(0, IntegrityAttemptType.Homework, 100, "BrowserTranslate", null, null, null);

            Assert.Empty(db.IntegrityViolationLogs);
        }

        [Fact]
        public async Task LogViolationAsync_EmptyViolationType_DefaultsToBrowserTranslate()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.Placement, 10, "", null, null, null);

            var log = await db.IntegrityViolationLogs.SingleAsync();
            Assert.Equal("BrowserTranslate", log.ViolationType);
            Assert.False(log.IsResolved);
        }

        [Fact]
        public async Task ResolveAsync_NonExistentId_ReturnsFalse()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            var result = await service.ResolveAsync(999, "admin-1", "غير موجود");

            Assert.False(result);
        }

        [Fact]
        public async Task ResolveAsync_ExistingViolation_MarksResolvedAndUnblocks()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.MinistrySim, 7, "BrowserTranslate", null, null, null);
            var log = await db.IntegrityViolationLogs.SingleAsync();

            var result = await service.ResolveAsync(log.Id, "admin-42", "تم التحقق يدويًا");

            Assert.True(result);

            var updated = await db.IntegrityViolationLogs.FindAsync(log.Id);
            Assert.True(updated!.IsResolved);
            Assert.Equal("admin-42", updated.ResolvedByAdminId);
            Assert.Equal("تم التحقق يدويًا", updated.ResolutionNote);
            Assert.NotNull(updated.ResolvedAt);

            // بعد الحل، لا يجب أن تُحظر المحاولة نفسها بعد الآن
            Assert.False(await service.IsBlockedAsync(1, IntegrityAttemptType.MinistrySim, 7));
        }

        [Fact]
        public async Task LogViolationAsync_AfterResolution_CanLogNewViolationForSameAttempt()
        {
            using var db = CreateContext();
            var service = new IntegrityGuardService(db);

            await service.LogViolationAsync(1, IntegrityAttemptType.EnhancementSkill, 9, "BrowserTranslate", null, null, null);
            var firstLog = await db.IntegrityViolationLogs.SingleAsync();
            await service.ResolveAsync(firstLog.Id, "admin-1", null);

            // يتجاوز حد معدّل تكرار المخالفات (5 ثوانٍ بين مخالفتين لنفس الطالب — Sprint 8/H3)
            // حتى لا يُرفَض التسجيل الثاني أدناه بسبب القرب الزمني من الأول داخل نفس الاختبار.
            firstLog.DetectedAt = DateTime.UtcNow.AddSeconds(-10);
            await db.SaveChangesAsync();

            // بعد إعادة الفتح، محاولة تحايل جديدة على نفس المحاولة يجب أن تُسجَّل من جديد (لا تُعتبر مكررة)
            await service.LogViolationAsync(1, IntegrityAttemptType.EnhancementSkill, 9, "BrowserTranslate", null, null, null);

            var totalRows = await db.IntegrityViolationLogs
                .CountAsync(v => v.StudentId == 1 && v.AttemptType == IntegrityAttemptType.EnhancementSkill && v.AttemptEntityId == 9);
            var activeRows = await db.IntegrityViolationLogs
                .CountAsync(v => v.StudentId == 1 && v.AttemptType == IntegrityAttemptType.EnhancementSkill && v.AttemptEntityId == 9 && !v.IsResolved);

            Assert.Equal(2, totalRows);
            Assert.Equal(1, activeRows);
            Assert.True(await service.IsBlockedAsync(1, IntegrityAttemptType.EnhancementSkill, 9));
        }
    }
}
