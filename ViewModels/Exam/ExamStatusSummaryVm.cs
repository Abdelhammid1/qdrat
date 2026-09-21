namespace QdratNew.ViewModels.Exam
{
    public class ExamStatusSummaryVm
    {
        public int Total { get; set; }          // إجمالي الاختبارات
        public int Required { get; set; }       // المطلوب حلها الآن
        public int Solved { get; set; }         // تم حلها
        public int Late { get; set; }           // متأخرة فعليًا
        public int ExpiringSoon { get; set; }   // سيتأخر قريبًا (خلال 48 ساعة)
        public int Expired { get; set; }        // انتهى وقته تمامًا
        public int BatchExams { get; set; }     // عدد اختبارات الدفعة
        public int IndividualExams { get; set; } // عدد الاختبارات الفردية






    }
}
