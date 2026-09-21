using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum PassageType
    {
        [Display(Name = "📄 نص")]
        Text = 0,

        [Display(Name = "🎧 صوت")]
        Audio = 1,

        [Display(Name = "🎥 فيديو")]
        Video = 2
    }

}
