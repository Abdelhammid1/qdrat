using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>RTK: المحور داخل الخطة (Section من المنهج) مع نموذجي الاختبار 101/102.</summary>
    public class RemedialTrackAxis
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public RemedialTrack? Track { get; set; }

        public int SectionId { get; set; }                 // المحور من المنهج
        public Section? Section { get; set; }
        [MaxLength(200)] public string? TitleOverride { get; set; }
        public int Order { get; set; }                     // 1..n

        public int Exam101ModelId { get; set; }            // ProfessionalModel (نوع Exam)
        public ProfessionalModel? Exam101Model { get; set; }
        public int Exam102ModelId { get; set; }
        public ProfessionalModel? Exam102Model { get; set; }
        public int ExamDurationMinutes { get; set; } = 30; // 5..180
        /// <summary>مُهمَل منذ RTK v2 (D19): المحور التالي يُفتح فور اجتياز السابق. العمود باقٍ لعدم الترحيل الهدّام.</summary>
        public int ReleaseDay { get; set; } = 1;

        public ICollection<RemedialTrackVideo> Videos { get; set; } = new List<RemedialTrackVideo>();
    }
}
