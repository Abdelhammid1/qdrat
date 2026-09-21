using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class Session
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب إدخال عنوان الجلسة")]
        public string Title { get; set; }

        [Required(ErrorMessage = "يجب إدخال مكان الجلسة")]
        public string Location { get; set; }

        [Required(ErrorMessage = "يجب إدخال تاريخ الجلسة")]
        public DateTime Date { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار المدرب")]
        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار المحور")]
        public int SectionId { get; set; }
        public Section Section { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار الدورة")]
        public int CourseId { get; set; }
        public Course Course { get; set; }

        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    }
}
