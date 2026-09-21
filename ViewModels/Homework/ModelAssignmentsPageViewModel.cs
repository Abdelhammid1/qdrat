using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class ModelAssignmentsPageViewModel
    {
        public List<ModelAssignmentViewModel> Homeworks { get; set; } = new();
        public List<ModelAssignmentViewModel> Exams { get; set; } = new();
        public List<ModelAssignmentViewModel> ArchivedHomeworks { get; set; } = new();
        public List<ModelAssignmentViewModel> ArchivedExams { get; set; } = new();
    }
}
