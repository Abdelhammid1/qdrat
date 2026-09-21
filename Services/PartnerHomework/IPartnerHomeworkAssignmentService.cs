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

        /// <summary>
        /// إرسال واجب مباشر اعتمادًا على قائمة أسئلة
        /// (Professional Model / Auto / Manual)
        /// </summary>
        void SendToStudentsWithQuestions(
            int partnerId,
            int subscriptionPeriodId,
            int courseId,
            string title,
            List<Guid> questionIds
        );
    }
}
