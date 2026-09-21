using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class StudyRoom
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم الغرفة")]
        [Display(Name = "اسم الغرفة")]
        public string RoomName { get; set; }

        [Required(ErrorMessage = "يرجى تحديد السعة")]
        [Display(Name = "السعة")]
        public int Capacity { get; set; }

        [Display(Name = "عدد الأجهزة المتاحة")]
        public int DeviceCount { get; set; }

        [Display(Name = "هل تحتوي على أجهزة؟")]
        public bool HasComputers { get; set; }

        [Display(Name = "الموقع")]
        public string Location { get; set; }

        [Display(Name = "فعالة؟")]
        public bool IsActive { get; set; } = true;
    }
}
