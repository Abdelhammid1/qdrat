using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class AutoGenerationSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int QuestionCount { get; set; } = 0;

        public List<AutoGenerationLessonVm> Lessons { get; set; } = new();
    }


    public class AutoGenerationLessonVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int QuestionCount { get; set; } = 0;


    }


    public class CreateStudentExamVm
    {

        [Required(ErrorMessage = "يجب اختيار الفرع")]
        public int? BranchId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int? BatchId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الطالب")]
        public int StudentId { get; set; }

        public string? Title { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        public int? CurriculumId { get; set; }

        public bool UseProfessionalModel { get; set; }
        public int? ProfessionalModelId { get; set; }

        public bool UseAutoGeneration { get; set; }

        public DateTime? ScheduledDate { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsOnline { get; set; }
        public string? ReferenceCode { get; set; }

        // Dropdowns
        public List<BranchDropdownVm> Branches { get; set; } = new();
        public List<BatchDropdownVm> Batches { get; set; } = new();
        public List<StudentDropdownVm> Students { get; set; } = new();

        public List<CurriculumDropdownVm> Curriculums { get; set; } = new();
        public List<ProfessionalModelDropdownVm> ProfessionalModels { get; set; } = new();





        public List<AutoGenerationSectionVm> Sections { get; set; } = new();

    













    }

    // عناصر القوائم

    public class StudentDropdownVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class BranchDropdownVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class BatchDropdownVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class CurriculumDropdownVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

    public class ProfessionalModelDropdownVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
