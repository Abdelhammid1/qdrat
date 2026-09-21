namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerHomeworkKpiViewModel
    {
        public int SentHomeworks { get; set; }
        public int UnsolvedHomeworks { get; set; }

        public decimal AverageCommitmentRate { get; set; }

        public string? MostNeglectedBatchName { get; set; }
    }
}
