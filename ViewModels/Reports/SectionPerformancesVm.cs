namespace QdratNew.ViewModels.Reports
{
    public class SectionPerformancesVm
    {
        public string SectionTitle { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }

        public int SectionId { get; set; }



        // 🔹 خاصية إضافية للحسابات في الجدول أو التشارت
        public double AccuracyPercent
        {
            get
            {
                if (Total == 0) return 0;
                return Math.Round((double)Correct / Total * 100, 1);
            }
        }
    }
}
