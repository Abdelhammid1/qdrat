using System;

namespace QdratNew.ViewModels.Admin
{
    public class FrontendLeadAdminVM
    {
        public int Id { get; set; }

        public string StudentName { get; set; }
        public string PhoneNumber { get; set; }

        public string CourseTitle { get; set; }
        public string? SubCourseTitle { get; set; }
        public string SelectedProgram { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsContacted { get; set; }
    }
}
