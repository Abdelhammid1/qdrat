namespace QdratNew.ViewModels.Remedial
{
    public class RemedialPlanListVm
    {
        public int Id { get; set; }
        public int StudentID { get; set; }

        public string Title { get; set; }
        public string StudentName { get; set; }
        public string CurriculumTitle { get; set; }
        public string WeakSections { get; set; }
        public string PerformanceLevel { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        // ✅ القائمة الفعلية للمحاور الضعيفة (المودال هيعتمد عليها)
        public List<string> WeakSectionList { get; set; } = new();
        public List<WeakSectionDetailVm> WeakSectionDetails { get; set; } = new();
   
    }
}
