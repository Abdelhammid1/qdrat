using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminPermissionModule
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Key { get; set; }   // Users, Students, Exams ...

        [Required, MaxLength(150)]
        public string NameAr { get; set; } // المستخدمون، الطلاب...

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
