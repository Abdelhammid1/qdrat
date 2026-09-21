namespace QdratNew.Entities
{
    public class HomeworkSetSection
    {
        public int Id { get; set; }
        public int HomeworkSetId { get; set; }
        public int SectionId { get; set; }

        public HomeworkSet HomeworkSet { get; set; }
        public Section Section { get; set; }
    }

}
