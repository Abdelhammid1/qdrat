using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class ProfessionalCertificateSectionSetting
{
    public int Id { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Subtitle { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ButtonText { get; set; }

    public bool AutoCloseWhenMaxReached { get; set; } = false;
    public int? GlobalMaxRequests { get; set; }

    [MaxLength(500)]
    public string? ClosedMessage { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }
}
