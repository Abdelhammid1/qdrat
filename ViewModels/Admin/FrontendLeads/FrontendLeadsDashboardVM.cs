using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.FrontendLeads
{
    // RL-S5 — أرقام الكروت (تُرجَع أيضًا من UpdateStatus لتحديث الكروت فورًا)
    public class FrontendLeadCountsVM
    {
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ContactedCount { get; set; }
        public int NeedFollowUpCount { get; set; }
        public int RejectedCount { get; set; }
        public int ConvertedCount { get; set; }
        public int TodayCount { get; set; }
        public int Last7DaysCount { get; set; }
    }

    public class FrontendLeadsDashboardVM
    {
        public FrontendLeadCountsVM Counts { get; set; } = new();
        public string? InitialStatusFilter { get; set; }
        public List<LeadProjectCountVM> ProjectCounts { get; set; } = new();
        public List<FrontendLeadRowVM> Leads { get; set; } = new();
    }

    public class LeadProjectCountVM
    {
        public int? ProjectId { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public class FrontendLeadRowVM
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public LeadApplicantType ApplicantType { get; set; }
        public string? ParentName { get; set; }
        public string? ParentPhone { get; set; }
        public string? City { get; set; }
        public string? SchoolStage { get; set; }
        public string? Notes { get; set; }
        public string? AdminNotes { get; set; }
        public FrontendLeadStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ContactedAt { get; set; }
        public bool IsLegacy { get; set; }
        public string? LegacyProgram { get; set; }
        public List<LeadCourseChipVM> Courses { get; set; } = new();
    }

    public class LeadCourseChipVM
    {
        public int? ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string CourseName { get; set; } = "";
    }

    public class UpdateLeadStatusRequest
    {
        [Required] public int Id { get; set; }
        [Required] public FrontendLeadStatus Status { get; set; }
        [StringLength(1000)] public string? AdminNotes { get; set; }
    }

    public class UpdateLeadStatusResult
    {
        public bool Found { get; set; }
        public FrontendLeadCountsVM Counts { get; set; } = new();
        public DateTime? ContactedAt { get; set; }
    }
}
