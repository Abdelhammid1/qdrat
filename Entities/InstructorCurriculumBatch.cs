using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Entities;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class InstructorCurriculumBatch
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب اختيار المدرب")]
        public int InstructorId { get; set; }
        [ForeignKey("InstructorId")]
        public Instructor Instructor { get; set; }

        [Required(ErrorMessage = "يجب اختيار المنهج")]
        public int CurriculumId { get; set; }
        [ForeignKey("CurriculumId")]
        public Curriculum Curriculum { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int BatchId { get; set; }
        [ForeignKey("BatchId")]
        public Batch Batch { get; set; }

        public string UserId { get; set; }



    }
}
