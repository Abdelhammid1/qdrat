using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using System.Security.Cryptography;

namespace QdratNew.Services.Exams.Implementations
{
    // Sprint 8 (MSE-E): إسناد اختبار معمل القياس لدفعة/دفعات أو لطالب/طلاب محددين + فرض محاولة واحدة فقط (E4)
    // Sprint 17 (MSE-J): وضع الحضور أونلاين/حضوري + توليد رمز مرجعي 6 أرقام آمن لكل عملية إسناد حضورية
    public class MinistrySimExamAssignmentService : IMinistrySimExamAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public MinistrySimExamAssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        // E1: إسناد لدفعة/دفعات — يتطلب IsPublished == true (القرار الملزم بالقسم 8 من الملف التنفيذي).
        // يُسجَّل رابط دفعة واحد لكل دفعة، ثم تُسنَد الأسئلة تلقائيًا لكل طالب مسجَّل فعليًا (Status = Active) في هذه الدفعات
        // (يُستثنى: طالب مُسنَد له الاختبار فرديًا بالفعل، أو طالب لديه محاولة مسجَّلة أصلاً لهذا الاختبار — القرار #7 / E4).
        public async Task<MinistrySimExamAssignmentResult> AssignToBatchesAsync(int ministrySimExamId, List<int> batchIds, int? createdByInstructorId, bool isOnline = true)
        {
            var result = new MinistrySimExamAssignmentResult();

            if (batchIds == null || batchIds.Count == 0)
            {
                result.Message = "يجب اختيار دفعة واحدة على الأقل.";
                return result;
            }

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
            {
                result.Message = "لم يتم العثور على اختبار معمل القياس المطلوب.";
                return result;
            }

            if (!exam.IsPublished)
            {
                result.Message = "لا يمكن إسناد الاختبار لدفعة إلا بعد نشره أولاً.";
                return result;
            }

            // فقط الدفعات التابعة فعليًا لدورة الاختبار، غير المحذوفة/المؤرشفة
            var validBatchIds = await _context.Batches
                .AsNoTracking()
                .Where(b => EF.Constant(batchIds).Contains(b.Id) && b.CourseId == exam.CourseId && !b.IsDeleted && !b.IsArchived)
                .Select(b => b.Id)
                .ToListAsync();

            if (validBatchIds.Count == 0)
            {
                result.Message = "الدفعات المختارة غير تابعة لدورة هذا الاختبار أو غير موجودة/مؤرشفة.";
                return result;
            }

            var alreadyAssignedBatchIds = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == ministrySimExamId && EF.Constant(validBatchIds).Contains(x.BatchId))
                .Select(x => x.BatchId)
                .ToListAsync();

            var newBatchIds = validBatchIds.Except(alreadyAssignedBatchIds).ToList();

            // Sprint 17 (MSE-J / J1/J2): رمز مرجعي واحد فقط لكل عملية إسناد حضورية (جلسة معمل واحدة = رمز واحد يُعلَن
            // شفهيًا لكل الحاضرين) — يُطبَّق على كل الدفعات الجديدة المُسنَدة في نفس هذه العملية.
            string generatedCode = null;
            if (!isOnline && newBatchIds.Any())
                generatedCode = await GenerateReferenceCodeAsync(ministrySimExamId);

            if (newBatchIds.Any())
            {
                var batchAssignments = newBatchIds.Select(batchId => new MinistrySimExamAssignmentToBatch
                {
                    MinistrySimExamId = ministrySimExamId,
                    BatchId = batchId,
                    IsSentToStudents = true,
                    CreatedByInstructorId = createdByInstructorId,
                    IsOnline = isOnline,
                    ReferenceCode = generatedCode
                }).ToList();

                await _context.BulkInsertAsync(batchAssignments);
            }

            result.AssignedBatchesCount = newBatchIds.Count;
            result.GeneratedReferenceCode = generatedCode;
            foreach (var skippedBatchId in alreadyAssignedBatchIds)
                result.SkippedMessages.Add($"الدفعة #{skippedBatchId} مُسنَدة لهذا الاختبار بالفعل.");

            // كل الطلاب المسجَّلين فعليًا (Status = Active) في كل الدفعات المختارة (جديدة أو مُسنَدة سابقًا)
            // — يشمل هذا الطلاب الجدد الذين انضموا للدفعة بعد إسنادها أول مرة.
            var enrolledStudentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => EF.Constant(validBatchIds).Contains(e.BatchId) && e.Status == "Active")
                .Select(e => e.StudentID)
                .Distinct()
                .ToListAsync();

            // ملاحظة تصميمية (Sprint 17): الصف الفردي لكل طالب مُسنَد عبر دفعة يحمل نفس IsOnline/ReferenceCode
            // لدفعته وقت إسنادها (وليس دفعته الحالية إن تغيّرت لاحقًا) — هذا يجعل MinistrySimExamAssignmentToStudent
            // مصدر الحقيقة الوحيد الذي تفحصه ValidateReferenceCodeAsync، بدل ازدواج الفحص بين جدولي الدفعة والطالب.
            var assignedCount = await AssignStudentsInternalAsync(ministrySimExamId, enrolledStudentIds, "Batch", result.SkippedMessages, isOnline, generatedCode);
            result.AssignedStudentsCount = assignedCount;

            result.Success = true;
            result.Message = $"تم إسناد الاختبار إلى {newBatchIds.Count} دفعة جديدة، و{assignedCount} طالب.";
            return result;
        }

        // E2: إسناد لطالب/طلاب محددين — يتطلب IsPublished == true، ويُقبل فقط الطلاب المسجَّلين فعليًا في دورة هذا الاختبار.
        public async Task<MinistrySimExamAssignmentResult> AssignToStudentsAsync(int ministrySimExamId, List<int> studentIds, bool isOnline = true)
        {
            var result = new MinistrySimExamAssignmentResult();

            if (studentIds == null || studentIds.Count == 0)
            {
                result.Message = "يجب اختيار طالب واحد على الأقل.";
                return result;
            }

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
            {
                result.Message = "لم يتم العثور على اختبار معمل القياس المطلوب.";
                return result;
            }

            if (!exam.IsPublished)
            {
                result.Message = "لا يمكن إسناد الاختبار لطالب إلا بعد نشره أولاً.";
                return result;
            }

            // فقط الطلاب المسجَّلين فعليًا (Status = Active) في دفعة تابعة لدورة هذا الاختبار
            var validStudentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => EF.Constant(studentIds).Contains(e.StudentID) && e.Status == "Active" && e.Batch.CourseId == exam.CourseId)
                .Select(e => e.StudentID)
                .Distinct()
                .ToListAsync();

            if (validStudentIds.Count == 0)
            {
                result.Message = "الطلاب المختارون غير مسجَّلين في دورة هذا الاختبار.";
                return result;
            }

            foreach (var invalidStudentId in studentIds.Except(validStudentIds))
                result.SkippedMessages.Add($"الطالب #{invalidStudentId} غير مسجَّل في دورة هذا الاختبار.");

            // Sprint 17 (MSE-J / J1/J2): رمز مرجعي واحد لكل عملية إسناد فردية حضورية (يُولَّد فقط إن وُجد طالب فعلي سيُسنَد له)
            string generatedCode = !isOnline && validStudentIds.Any()
                ? await GenerateReferenceCodeAsync(ministrySimExamId)
                : null;

            var assignedCount = await AssignStudentsInternalAsync(ministrySimExamId, validStudentIds, "Official", result.SkippedMessages, isOnline, generatedCode);
            result.AssignedStudentsCount = assignedCount;
            result.GeneratedReferenceCode = assignedCount > 0 ? generatedCode : null;

            result.Success = true;
            result.Message = $"تم إسناد الاختبار إلى {assignedCount} طالب.";
            return result;
        }

        // إسناد لطلاب ضيوف — بلا أي تحقق انتماء لدورة (الضيف خارج نظام الدورات/الدفعات أصلاً بالتعريف)،
        // يتطلب فقط IsPublished == true ووجود الضيف فعليًا (IsActive == true).
        public async Task<MinistrySimExamAssignmentResult> AssignToGuestsAsync(int ministrySimExamId, List<int> guestStudentIds)
        {
            var result = new MinistrySimExamAssignmentResult();

            if (guestStudentIds == null || guestStudentIds.Count == 0)
            {
                result.Message = "يجب اختيار ضيف واحد على الأقل.";
                return result;
            }

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
            {
                result.Message = "لم يتم العثور على اختبار معمل القياس المطلوب.";
                return result;
            }

            if (!exam.IsPublished)
            {
                result.Message = "لا يمكن إسناد الاختبار لضيف إلا بعد نشره أولاً.";
                return result;
            }

            var validGuestIds = await _context.MinistrySimExamGuestStudents
                .AsNoTracking()
                .Where(g => EF.Constant(guestStudentIds).Contains(g.Id) && g.IsActive)
                .Select(g => g.Id)
                .ToListAsync();

            if (validGuestIds.Count == 0)
            {
                result.Message = "الضيوف المختارون غير موجودين أو غير نشطين.";
                return result;
            }

            var alreadyAssignedGuestIds = await _context.MinistrySimExamAssignmentsToGuests
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == ministrySimExamId && EF.Constant(validGuestIds).Contains(x.GuestStudentId))
                .Select(x => x.GuestStudentId)
                .ToListAsync();

            foreach (var guestId in alreadyAssignedGuestIds)
                result.SkippedMessages.Add($"الضيف #{guestId} مُسنَد له هذا الاختبار بالفعل.");

            var guestsToAssign = validGuestIds.Except(alreadyAssignedGuestIds).ToList();

            if (guestsToAssign.Any())
            {
                var guestAssignments = guestsToAssign.Select(guestId => new MinistrySimExamAssignmentToGuest
                {
                    MinistrySimExamId = ministrySimExamId,
                    GuestStudentId = guestId
                }).ToList();

                await _context.BulkInsertAsync(guestAssignments);
            }

            result.Success = true;
            result.Message = $"تم إسناد الاختبار إلى {guestsToAssign.Count} ضيف.";
            return result;
        }

        // يُدرج رابط MinistrySimExamAssignmentToStudent لكل طالب من studentIds، باستثناء:
        // - طالب مُسنَد له الاختبار بالفعل (لا تكرار).
        // - طالب لديه محاولة مسجَّلة أصلاً لهذا الاختبار (القرار #7 / E4 — الفهرس الفريد من Sprint 1 هو الضامن النهائي،
        //   وهذا تحقق صريح إضافي عند الإسناد نفسه بدل ترك الخطأ يظهر لاحقًا عند بدء المحاولة).
        private async Task<int> AssignStudentsInternalAsync(int ministrySimExamId, List<int> studentIds, string sourceType, List<string> skippedMessages, bool isOnline = true, string referenceCode = null)
        {
            if (studentIds == null || studentIds.Count == 0)
                return 0;

            var alreadyAssignedStudentIds = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == ministrySimExamId && EF.Constant(studentIds).Contains(x.StudentId))
                .Select(x => x.StudentId)
                .ToListAsync();

            var studentsWithAttempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == ministrySimExamId && EF.Constant(studentIds).Contains(x.StudentId))
                .Select(x => x.StudentId)
                .ToListAsync();

            var studentsToAssign = studentIds
                .Except(alreadyAssignedStudentIds)
                .Except(studentsWithAttempt)
                .ToList();

            foreach (var studentId in alreadyAssignedStudentIds)
                skippedMessages.Add($"الطالب #{studentId} مُسنَد له هذا الاختبار بالفعل.");

            foreach (var studentId in studentsWithAttempt.Except(alreadyAssignedStudentIds))
                skippedMessages.Add($"الطالب #{studentId} لديه محاولة مسجَّلة أصلاً لهذا الاختبار — لا يمكن إسناده مرة أخرى.");

            if (studentsToAssign.Count == 0)
                return 0;

            var studentAssignments = studentsToAssign.Select(studentId => new MinistrySimExamAssignmentToStudent
            {
                MinistrySimExamId = ministrySimExamId,
                StudentId = studentId,
                SourceType = sourceType,
                IsOnline = isOnline,
                ReferenceCode = referenceCode
            }).ToList();

            await _context.BulkInsertAsync(studentAssignments);

            return studentsToAssign.Count;
        }

        // Sprint 17 (MSE-J / J1): توليد رمز مرجعي عشوائي آمن (RandomNumberGenerator وليس Random العادي) من 6 أرقام،
        // بمحاولة تفرّد ضمن نفس الاختبار فقط (النطاق الفعلي محدود بجلسة معمل واحدة — لا حاجة لتفرّد عالمي).
        private async Task<string> GenerateReferenceCodeAsync(int ministrySimExamId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                var exists = await _context.MinistrySimExamAssignmentsToBatches
                    .AsNoTracking()
                    .AnyAsync(x => x.MinistrySimExamId == ministrySimExamId && x.ReferenceCode == code)
                    || await _context.MinistrySimExamAssignmentsToStudents
                    .AsNoTracking()
                    .AnyAsync(x => x.MinistrySimExamId == ministrySimExamId && x.ReferenceCode == code);

                if (!exists)
                    return code;
            }

            return DateTime.UtcNow.Ticks.ToString()[^6..];
        }
    }
}
