using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class StudyRoomViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم الغرفة")]
        [Display(Name = "اسم الغرفة")]
        public string RoomName { get; set; }

        [Required(ErrorMessage = "يرجى تحديد السعة")]
        [Display(Name = "السعة القصوى")]
        public int Capacity { get; set; }

        [Display(Name = "عدد الأجهزة")]
        public int? DeviceCount { get; set; }

        [Display(Name = "هل تحتوي على أجهزة؟")]
        public bool HasComputers { get; set; }

        [Display(Name = "الموقع")]
        public string Location { get; set; }

        [Display(Name = "نشطة؟")]
        public bool IsActive { get; set; } = true;
    }
}
