using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    // APD-S1: جمهور الإرسال في شاشات الأدمن. الافتراضي «المنصة» = السلوك قبل APD على دفعات المعهد فقط.
    public enum DispatchAudience
    {
        [Display(Name = "دفعات المنصة")]
        Platform = 1,

        [Display(Name = "المدارس الشريكة")]
        Partners = 2
    }
}
