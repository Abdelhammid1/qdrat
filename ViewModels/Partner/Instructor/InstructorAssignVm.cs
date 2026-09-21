using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Instructor
{
    public class InstructorAssignVm
    {
        public int InstructorId { get; set; }

        public List<int> SelectedBatchIds { get; set; } = new();
        public int? SelectedCurriculumId { get; set; }
        public List<InstructorBatchRoleType> SelectedRoles { get; set; } = new();

        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Curriculums { get; set; } = new();

        public List<CurrentAssignmentVm> CurrentAssignments { get; set; } = new();
    }

    public class CurrentAssignmentVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public string CurriculumName { get; set; }

        public string RoleName { get; set; }
    }
}