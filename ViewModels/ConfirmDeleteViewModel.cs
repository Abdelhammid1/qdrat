namespace QdratNew.ViewModels
{
    public class ConfirmDeleteViewModel
    {
        public Guid ItemId { get; set; }        // ID العنصر
        public string ItemName { get; set; }    // الاسم الظاهر في الرسالة
        public string Controller { get; set; }  // اسم الكنترولار


        public string ModalId { get; set; }             // معرف المودال مثل "confirmDeleteModal"
        public string Title { get; set; }               // عنوان المودال مثل "تأكيد الحذف"
        public string Message { get; set; }             // الرسالة مثل "هل أنت متأكد من حذف هذا السؤال؟"
        public string ConfirmButtonText { get; set; }   // نص زر التأكيد مثل "نعم، احذف"
        public string CancelButtonText { get; set; }    // نص زر الإلغاء مثل "إلغاء"



    }
}
