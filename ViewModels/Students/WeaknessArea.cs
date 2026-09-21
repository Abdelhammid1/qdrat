namespace QdratNew.ViewModels.Students
{
    public class WeaknessArea
    {
        public string SectionName { get; set; } = "";
        public double AverageScore { get; set; }

        // معرف المحور أو القسم (SectionId)
        public int SectionId { get; set; }


        // نسبة الدقة / مستوى الأداء
        public double Accuracy { get; set; }
    }
}
