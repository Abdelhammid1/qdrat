using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Dashboard
{
    public class AdminDrillDownPageViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string BackUrl { get; set; } = "/Admin/AdminOperationsDashboard";

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public List<AdminDrillDownKpiViewModel> Kpis { get; set; } = new List<AdminDrillDownKpiViewModel>();

        public List<AdminDrillDownColumnViewModel> Columns { get; set; } = new List<AdminDrillDownColumnViewModel>();

        public List<AdminDrillDownRowViewModel> Rows { get; set; } = new List<AdminDrillDownRowViewModel>();

        public string EmptyMessage { get; set; } = "لا توجد بيانات متاحة.";
    }

    public class AdminDrillDownKpiViewModel
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Hint { get; set; } = string.Empty;

        public string CssClass { get; set; } = "info";
    }

    public class AdminDrillDownColumnViewModel
    {
        public string Key { get; set; } = string.Empty;

        public string Header { get; set; } = string.Empty;
    }

    public class AdminDrillDownRowViewModel
    {
        public Dictionary<string, string> Cells { get; set; } = new Dictionary<string, string>();

        public string RowCssClass { get; set; } = string.Empty;

        public string? ActionUrl { get; set; }

        public string ActionText { get; set; } = "فتح";

        /// <summary>معرف الطالب — يُفعّل الـ footer الطافي للإجراءات عند وجوده</summary>
        public int StudentId { get; set; } = 0;

        /// <summary>معرف المستخدم المرتبط بالطالب في جدول AspNetUsers</summary>
        public string? UserId { get; set; }

        /// <summary>اسم الكيان (الطالب) للعرض في الـ footer</summary>
        public string? EntityName { get; set; }
    }

    public class LiveStudentSnapshot
    {
        public int StudentId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CurrentPath { get; set; } = string.Empty;

        public string CurrentPageTitle { get; set; } = string.Empty;

        public DateTime FirstSeenAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public int PageHitCount { get; set; }

        public bool IsLiveNow { get; set; }

        public bool IsMoving { get; set; }
    }
}