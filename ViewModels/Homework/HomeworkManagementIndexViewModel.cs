using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkManagementIndexViewModel
    {
        public List<HomeworkOverviewViewModel> Homeworks { get; set; }
        public List<HomeworkOverviewViewModel> ArchivedHomeworks { get; set; } = new();
        public List<SelectListItem> Batches { get; set; }
        public int? SelectedBatchId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int QuestionsPerStudent { get; set; }
        public bool ShowArchived { get; set; }
        public string CompletionTitle { get; set; }

        // بطاقات الدفعات للعرض الجديد
        public List<HomeworkBatchCardVM> BatchCards { get; set; } = new();

        // إحصائيات KPI إجمالية
        public int TotalBatches { get; set; }
        public int TotalHomeworksCount { get; set; }
        public int TotalCriticalStudents { get; set; }
        public int TotalLate24hStudents { get; set; }
    }
}
