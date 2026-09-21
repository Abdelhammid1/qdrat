using System;

namespace QdratNew.Entities
{
    public class UserLoginLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
        public DateTime LoginAt { get; set; }
        public DateTime? LogoutAt { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
    }
}
