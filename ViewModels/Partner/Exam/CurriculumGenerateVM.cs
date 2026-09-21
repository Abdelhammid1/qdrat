namespace QdratNew.ViewModels.Partner.Exam
{
    public class CurriculumGenerateVM
    {
        public int CurriculumId { get; set; }

        // عدد الأسئلة من هذا المنهج
        public int QuestionCount { get; set; }

        // هل كل المحاور؟
        public bool UseAllSections { get; set; }

        // في حالة تحديد محاور
        public List<SectionGenerateVM> Sections { get; set; }
            = new();



    }
}
