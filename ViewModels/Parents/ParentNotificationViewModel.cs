using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Parents
{
    public class ParentNotificationViewModel
    {
        public List<ParentNotificationItemViewModel> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}
