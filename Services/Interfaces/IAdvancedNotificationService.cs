using QdratNew.Enums;

namespace QdratNew.Services.Interfaces
{
    public interface IAdvancedNotificationService
    {
        Task SendToStudentAsync(int studentId, string message, NotificationCategory category, string? targetUrl = null, int? sentByInstructorId = null);
        Task SendToStudentsAsync(List<int> studentIds, string message, NotificationCategory category, string? targetUrl = null, int? sentByInstructorId = null);
        Task SendToUserAsync(string userId, string message, NotificationCategory category, string? targetUrl = null);
        Task SendToRoleAsync(string role, string message, NotificationCategory category, string? targetUrl = null);
        Task SendToInstructorAsync(int instructorId, string message, NotificationCategory category, string? targetUrl = null);


    }
}
