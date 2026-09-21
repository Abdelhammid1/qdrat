using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Course
{
    public class CourseDetailsViewModel
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }
        public string BranchName  { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public bool   IsActive    { get; set; }

        // ── إحصائيات ─────────────────────────────
        public int TotalBatches    { get; set; }
        public int TotalStudents   { get; set; }
        public int TotalInstructors{ get; set; }
        public int TotalCurriculums{ get; set; }

        // ── قوائم ────────────────────────────────
        public List<CourseBatchSummary>  Batches     { get; set; } = new();
        public List<string>              Instructors { get; set; } = new();
        public List<string>              Curriculums { get; set; } = new();

        // ── مشتقة ────────────────────────────────
        public int DurationDays =>
            EndDate > StartDate ? (EndDate - StartDate).Days : 0;

        public string StatusLabel  => IsActive ? "نشطة" : "موقوفة";
        public string StatusClass  => IsActive ? "active" : "inactive";
    }

    public class CourseBatchSummary
    {
        public int      Id            { get; set; }
        public string   Name          { get; set; } = "";
        public DateTime StartDate     { get; set; }
        public DateTime? EndDate      { get; set; }
        public int      StudentCount  { get; set; }
        public bool     IsActive      { get; set; }
        public bool     IsArchived    { get; set; }
        public string   GenderLabel   { get; set; } = "";

        public string StatusLabel =>
            IsArchived ? "مؤرشف" : IsActive ? "نشط" : "منتهي";
        public string StatusClass =>
            IsArchived ? "archived" : IsActive ? "active" : "ended";
    }
}
