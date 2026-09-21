using System.Threading.Tasks;
using QdratNew.Entities;

namespace QdratNew.Services.Exams.Interfaces
{
    // تجربة حل الطالب لاختبار معمل القياس — القفل أحادي الاتجاه بين المراحل (يُنفَّذ في Sprint 9/10 — MSE-F)
    public interface IMinistrySimExamAttemptService
    {
        // يرفض إن توجد محاولة سابقة مكتملة
        Task<MinistrySimExamStudentAttempt> StartOrResumeAttemptAsync(int ministrySimExamId, int studentId);

        // يرفض إن كانت مرحلة سابقة غير مقفلة بعد، أو إن كانت هذه المرحلة مقفلة أصلاً
        Task<MinistrySimExamStudentStageProgress> StartStageAsync(int attemptId, int stageNumber);

        Task SubmitAnswerAsync(int attemptId, int stageQuestionId, int? selectedOptionId, bool flagForReview);

        // القفل النهائي — العملية الأهم في كل الميزة، لا يوجد أي مسار كود يعيد IsLocked إلى false بعدها
        Task<MinistrySimExamStudentStageProgress> LockStageAsync(int attemptId, int stageNumber, bool timeExpired);

        // Sprint 10 (F3/F4): يُستدعى في بداية كل طلب على مرحلة مفتوحة — يقفلها تلقائيًا (TimeExpired = true) إن
        // تجاوز الوقت الحالي على الخادم (وليس الـ Client) مهلة StartedAt + DurationMinutes. يُرجع true فقط إذا
        // قفلها الآن بسبب انتهاء الوقت (لتمييزه عن مرحلة غير مبدوءة/مقفلة أصلاً لسبب آخر).
        Task<bool> EnforceStageTimeLimitAsync(int attemptId, int stageNumber);

        // Sprint 17 (MSE-J / J3): بوابة الدخول للاختبار الحضوري — يقارن enteredCode بالرمز المرجعي المُسنَد فعليًا
        // لهذا الطالب. IsOnline == true يتجاوز التحقق بالكامل (يُرجع true دائمًا) — كذلك عدم وجود إسناد فعلي أصلاً
        // (يُترك رفض الوصول لـ StartOrResumeAttemptAsync برسالته العربية الواضحة بدل حلقة تحقق بلا معنى هنا).
        Task<bool> ValidateReferenceCodeAsync(int ministrySimExamId, int studentId, string enteredCode);
    }
}
