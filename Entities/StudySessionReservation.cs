using QdratNew.Entities;
using QdratNew.Entities;
using QdratNew.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{ 
public class StudySessionReservation
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Student")]
    public int StudentID { get; set; }
    public Student Student { get; set; }

    [ForeignKey("Branch")]
    public int BranchId { get; set; }
    public Branch Branch { get; set; }

    public DateTime RequestedDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public StudySessionStatus Status { get; set; } = StudySessionStatus.Pending;

    [ForeignKey("Instructor")]
    public int? InstructorId { get; set; }
    public Instructor Instructor { get; set; }

    public decimal? Fee { get; set; }

    public decimal? BaseFee { get; set; } // ✅ يحددها الأدمن عند الموافقة
    public decimal? FinalFee { get; set; } // ✅ تحسب بعد الجلسة

    public string AdditionalServices { get; set; }
    public string Notes { get; set; } // ✅ جديد
    public string RoomName { get; set; } // ✅ جديد

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TotalSeats { get; set; }
    public int ReservedSeats { get; set; }

    public bool IsApproved { get; set; } = false;
    public bool IsCompleted { get; set; } = false;

    public string PaymentReference { get; set; } // ❓ اختياري حسب الخطة

        // ✅ ربط الجلسة بغرفة مذاكرة
        [ForeignKey("StudyRoom")]
        public int? StudyRoomId { get; set; }
        public StudyRoom StudyRoom { get; set; }

        // ✅ تحديد جنس الجلسة لمنع الاختلاط
        [Required]
        public GenderType Gender { get; set; }


    }
}