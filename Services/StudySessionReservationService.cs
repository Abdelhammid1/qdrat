using System;
using System.Linq;
using System.Threading.Tasks;
using QdratNew.Data;
using QdratNew.Entities;
using Microsoft.EntityFrameworkCore;

namespace QdratNew.Services
{
    public class StudySessionReservationService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public StudySessionReservationService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // 🔹 تقديم طلب لحجز جلسة مذاكرة
        public async Task<bool> RequestStudySession(int studentId, DateTime requestedDate, TimeSpan startTime, TimeSpan endTime, decimal fee, string additionalServices)
        {
            var session = await _context.StudySessionReservations
                .Where(r => r.RequestedDate == requestedDate && r.StartTime == startTime)
                .FirstOrDefaultAsync();

            // ✅ التحقق من توفر مقاعد
            if (session != null && session.ReservedSeats >= session.TotalSeats)
            {
                _notificationService.SendNotification(studentId, "⛔ لا توجد أماكن متاحة لهذه الجلسة، يرجى اختيار موعد آخر.", "حجوزات");
                return false; // ❌ لا يوجد مقاعد متاحة
            }

            var reservation = new StudySessionReservation
            {
                StudentID = studentId,
                RequestedDate = requestedDate,
                StartTime = startTime,
                EndTime = endTime,
                Fee = fee,
                AdditionalServices = additionalServices,
                TotalSeats = session?.TotalSeats ?? 10, // ✅ افتراضيًا 10 مقاعد إذا لم تكن موجودة
                ReservedSeats = session != null ? session.ReservedSeats + 1 : 1
            };

            _context.StudySessionReservations.Add(reservation);
            await _context.SaveChangesAsync();

            // ✅ إشعار الطالب بتأكيد استلام طلبه
            _notificationService.SendNotification(studentId, "📢 تم تقديم طلب حجز جلسة مذاكرة. انتظر موافقة الإدارة.", "حجوزات");

            // ✅ إشعار الإدارة بوجود طلب جديد
            _notificationService.SendNotification(0, "📢 يوجد طلب جديد لحجز جلسة مذاكرة، يرجى مراجعته.", "إدارة");

            return true; // ✅ تمت إضافة الحجز بنجاح
        }

        // 🔹 الموافقة على طلب الحجز من قبل الإدارة
        public async Task<bool> ApproveStudySession(int reservationId)
        {
            var reservation = await _context.StudySessionReservations.FindAsync(reservationId);
            if (reservation == null || reservation.IsApproved)
                return false; // ❌ الطلب غير موجود أو تمت الموافقة عليه مسبقًا

            reservation.IsApproved = true;
            await _context.SaveChangesAsync();

            // ✅ إشعار الطالب بأن طلبه تمت الموافقة عليه
            _notificationService.SendNotification(reservation.StudentID, "✅ تمت الموافقة على طلب حجز جلسة المذاكرة الخاصة بك!", "حجوزات");

            return true;
        }

        // 🔹 تسجيل حضور الطالب في الجلسة
        public async Task<bool> CompleteStudySession(int reservationId)
        {
            var reservation = await _context.StudySessionReservations.FindAsync(reservationId);
            if (reservation == null || !reservation.IsApproved || reservation.IsCompleted)
                return false; // ❌ لا يمكن تسجيل الحضور

            reservation.IsCompleted = true;
            await _context.SaveChangesAsync();

            // ✅ إشعار الطالب بأنه أكمل الجلسة
            _notificationService.SendNotification(reservation.StudentID, "🎓 لقد أكملت جلسة المذاكرة بنجاح. نتمنى لك دراسة موفقة!", "حجوزات");

            return true;
        }

        public async Task<bool> CancelStudySession(int reservationId, int studentId)
        {
            var reservation = await _context.StudySessionReservations
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.StudentID == studentId);

            if (reservation == null || reservation.IsCompleted)
                return false; // ❌ لا يمكن إلغاء جلسة غير موجودة أو مكتملة

            if (reservation.IsApproved && DateTime.Now >= reservation.RequestedDate)
                return false; // ❌ لا يمكن إلغاء الجلسة بعد أن يبدأ وقتها

            // ✅ تحرير المقعد المحجوز
            if (reservation.ReservedSeats > 0)
            {
                reservation.ReservedSeats--;
            }

            _context.StudySessionReservations.Remove(reservation);
            await _context.SaveChangesAsync();

            // ✅ إشعار الطالب بإلغاء الجلسة
            _notificationService.SendNotification(studentId, "❌ تم إلغاء جلسة المذاكرة الخاصة بك بنجاح.", "حجوزات");

            // ✅ إشعار الإدارة لإعادة إتاحة المقعد لطالب آخر
            _notificationService.SendNotification(0, $"📢 تم إلغاء جلسة مذاكرة لطالب ID: {studentId}. يمكن الآن إتاحة المقعد لطالب آخر.", "إدارة");

            return true; // ✅ تم إلغاء الحجز بنجاح
        }


    }
}
