using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Parents
{
    public class ParentMessageViewModel
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string Status { get; set; } = "New";
        public string StatusLabel { get; set; } = "جديدة";
        public string StatusColor { get; set; } = "primary";
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
        public bool HasReply { get; set; }
        public string? ReplyBody { get; set; }
        public string? RepliedAt { get; set; }
        public string StudentName { get; set; } = string.Empty;
    }

    public class ParentMessageCreateViewModel
    {
        [Required]
        public int StudentId { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string MessageBody { get; set; } = string.Empty;

        public List<ParentChildCardViewModel> AvailableStudents { get; set; } = new();

        public List<string> SubjectSuggestions { get; set; } = new()
        {
            "استفسار عن مستوى الطالب",
            "مشكلة في الالتزام",
            "سؤال عن واجب",
            "سؤال عن اختبار",
            "طلب نصيحة تربوية"
        };
    }

    public class ParentMessagesListViewModel
    {
        public List<ParentMessageViewModel> Messages { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnrepliedCount { get; set; }
    }
}
