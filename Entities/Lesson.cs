using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class Lesson
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان الدرس مطلوب")]
        public string Title { get; set; }

        public string Content { get; set; }

        [Required]
        public int UnitId { get; set; }
        public Unit Unit { get; set; }

        [Required]
        public int SectionId { get; set; }  // الربط بالمحور (مؤشر)
        public Section Section { get; set; }

    
        public int? LectureId { get; set; }  // الربط بالمحاضرة
        public Lecture Lecture { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<BatchLessonCompletion> BatchCompletions { get; set; } = new List<BatchLessonCompletion>();
    }
}
