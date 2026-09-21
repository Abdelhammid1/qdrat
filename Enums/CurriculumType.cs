   using System.ComponentModel.DataAnnotations;

    namespace QdratNew.Enums
    {
        public enum CurriculumType
        {
            [Display(Name = "كمي")]
            Quantitative = 0,

            [Display(Name = "لفظي")]
            Verbal = 1,

            [Display(Name = "تحصيلي / آخر")]
            Other = 2
        }
    }


