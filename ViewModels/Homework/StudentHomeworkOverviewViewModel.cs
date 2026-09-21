namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkOverviewViewModel
    {
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public int Pending { get; set; }

        public bool HasPending => Pending > 0;

    
        public int Late { get; set; } // ✅ الجديد
    }
}
