using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.Services.Exams.Models;

namespace QdratNew.Services.Exams.Interfaces
{
    // إسناد اختبار معمل القياس (دفعة/طالب فردي) — التنفيذ الفعلي في Sprint 8 (MSE-E)، بناءً على القرار #6 (ADR-MSE-4):
    // كيانات إسناد منفصلة تمامًا (MinistrySimExamAssignmentToBatch / MinistrySimExamAssignmentToStudent) بلا Discriminator موحّد.
    public interface IMinistrySimExamAssignmentService
    {
        // E1: إسناد لدفعة/دفعات — يرفض إن لم يكن الاختبار منشورًا (IsPublished == false)
        // Sprint 17 (MSE-J / J1/J2): isOnline يحدد أونلاين (فتح مباشر) أو حضوري (توليد رمز مرجعي واحد لكل دفعات هذه العملية)
        // APD-S5.1: ownerPartnerId = null ⇒ دفعات المعهد فقط (Branch.PartnerId == null)، وإلا دفعات هذه المدرسة وطلابها
        // النشطون فقط، وكل صف إسناد يحمل OwnerPartnerId. لا يفتح Transaction: المستدعي للمدارس يغلّفه بـ Transaction المدرسة.
        Task<MinistrySimExamAssignmentResult> AssignToBatchesAsync(int ministrySimExamId, List<int> batchIds, int? createdByInstructorId, bool isOnline = true, DateTime? visibleFrom = null, int? ownerPartnerId = null);

        // E2: إسناد لطالب/طلاب محددين — يرفض إن لم يكن الاختبار منشورًا (IsPublished == false)
        // Sprint 17 (MSE-J / J1/J2): isOnline يحدد أونلاين (فتح مباشر) أو حضوري (توليد رمز مرجعي واحد لكل طلاب هذه العملية)
        // APD-S5.1: ownerPartnerId = null ⇒ طلاب مسجلون في دفعة معهد من دورة الاختبار، وإلا طلاب نشطون من فروع المدرسة في دفعة لها.
        Task<MinistrySimExamAssignmentResult> AssignToStudentsAsync(int ministrySimExamId, List<int> studentIds, bool isOnline = true, DateTime? visibleFrom = null, int? ownerPartnerId = null);

        // إسناد لطالب/طلاب ضيوف (لا ينتمون لأي دورة/دفعة) — لا يوجد تحقق انتماء لدورة (الضيف أصلاً خارج نظام الدورات)
        Task<MinistrySimExamAssignmentResult> AssignToGuestsAsync(int ministrySimExamId, List<int> guestStudentIds);
    }
}
