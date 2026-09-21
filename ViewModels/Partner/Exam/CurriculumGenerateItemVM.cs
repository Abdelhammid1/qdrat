using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class CurriculumGenerateItemVM
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }

        // عدد الأسئلة من هذا المنهج
        public int QuestionCount { get; set; }

        // توزيع اختياري على المحاور
        public List<SectionGenerateItemVM> Sections { get; set; }
            = new List<SectionGenerateItemVM>();
        public bool UseAllSections { get; internal set; }




        // ✅ توزيع الصعوبة
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
        public int VeryHardCount { get; set; }

  

        // ✅ إجمالي الأسئلة (اختياري مفيد)
        public int TotalQuestions =>
            EasyCount + MediumCount + HardCount + VeryHardCount;



    }
}
