using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Curriculum
{
    public class CurriculumModuleViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }  // اسم الوحدة التعليمية

        [Required]
        public string Content { get; set; }  // محتوى الوحدة التعليمية

        public int CurriculumId { get; set; }  // ارتباط الوحدة بالمنهج
    }
}
