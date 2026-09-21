using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QdratNew.Entities;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels
{
    public class SessionFormViewModel
    {
        [ValidateNever]
        public List<QdratNew.Entities.Course> Courses { get; set; }
        [ValidateNever]
        public List<QdratNew.Entities.Section> Sections { get; set; }
        [ValidateNever]
        public List<QdratNew.Entities.Instructor> Instructors { get; set; }
        [ValidateNever]
        public Session Session { get; set; }

    }
}
