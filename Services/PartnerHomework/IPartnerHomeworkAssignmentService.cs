using QdratNew.ViewModels.Partner.HomeworkDraft;

namespace QdratNew.Services.PartnerHomework
{
    public interface IPartnerHomeworkAssignmentService
    {
        /// <summary>
        /// إرسال مسودة واجب إلى دفعات مختارة يدويًا
        /// </summary>
        void SendDraftToBatches(
            SendHomeworkDraftVM model,
            int partnerId,
            int subscriptionPeriodId
        );
    }
}
