namespace QdratNew.Security.AdminPermissions
{
    public static class AdminPermissionPolicies
    {
        // ===============================
        // Questions
        // ===============================
        public const string Questions_Read = "Questions:Read";
        public const string Questions_Add = "Questions:Add";
        public const string Questions_Edit = "Questions:Edit";
        public const string Questions_Delete = "Questions:Delete";
        public const string Questions_Approve = "Questions:Approve";
        public const string Questions_Reject = "Questions:Reject";
        public const string Questions_Preview = "Questions:Preview";
        public const string Questions_ViewAudit = "Questions:ViewAudit";

        // ===============================
        // Curriculums
        // ===============================
        public const string Curriculums_Read = "Curriculums:Read";
        public const string Curriculums_FilterList = "Curriculums:FilterList";

        // ===============================
        // Sections
        // ===============================
        public const string Sections_Read = "Sections:Read";
        public const string Sections_FilterList = "Sections:FilterList";

        // ===============================
        // Lessons
        // ===============================
        public const string Lessons_Read = "Lessons:Read";
        public const string Lessons_FilterList = "Lessons:FilterList";

        // ===============================
        // ExamAssignments
        // ===============================
        public const string Exams_Read = "ExamAssignments:Read";
        public const string Exams_Details = "ExamAssignments:Details";
        public const string Exams_Generate = "ExamAssignments:Generate";
        public const string Exams_Edit = "ExamAssignments:Edit";
        public const string Exams_Publish = "ExamAssignments:Publish";
        public const string Exams_Delete = "ExamAssignments:Delete";
        public const string Exams_Results = "ExamAssignments:Results";
        public const string Exams_Archive = "ExamAssignments:Archive";

        // ===============================
        // ExamIndividualAssignments
        // ===============================
        public const string ExamIndividualAssignments_Read = "ExamIndividualAssignments:Read";
        public const string ExamIndividualAssignments_Details = "ExamIndividualAssignments:Details";
        public const string ExamIndividualAssignments_Create = "ExamIndividualAssignments:Create";
        public const string ExamIndividualAssignments_Edit = "ExamIndividualAssignments:Edit";
        public const string ExamIndividualAssignments_Delete = "ExamIndividualAssignments:Delete";

        // ===============================
        // PlacementExams
        // ===============================
        public const string PlacementExams_Read = "PlacementExams:Read";
        public const string PlacementExams_Create = "PlacementExams:Create";
        public const string PlacementExams_Edit = "PlacementExams:Edit";
        public const string PlacementExams_Results = "PlacementExams:Results";
        public const string PlacementExams_Delete = "PlacementExams:Delete";
        public const string PlacementExams_Archive = "PlacementExams:Archive";

        // ===============================
        // AdminLessonCompletions
        // ===============================
        public const string AdminLessonCompletions_Read = "AdminLessonCompletions:Read";
        public const string AdminLessonCompletions_GenerateHomework = "AdminLessonCompletions:GenerateHomework";
        public const string AdminLessonCompletions_ManageModelHomework = "AdminLessonCompletions:ManageModelHomework";
        public const string AdminLessonCompletions_ManageModelExam = "AdminLessonCompletions:ManageModelExam";
        public const string AdminLessonCompletions_Enhancement = "AdminLessonCompletions:Enhancement";
        public const string AdminLessonCompletions_Delete = "AdminLessonCompletions:Delete";

        // ===============================
        // HomeworkManagement
        // ===============================
        public const string Homework_Read = "HomeworkManagement:Read";
        public const string Homework_Reports = "HomeworkManagement:Reports";
        public const string Homework_Details = "HomeworkManagement:Details";
        public const string Homework_Review = "HomeworkManagement:Review";
        public const string Homework_ManageQuestions = "HomeworkManagement:ManageQuestions";
        public const string Homework_AddQuestion = "HomeworkManagement:AddQuestion";
        public const string Homework_RemoveQuestion = "HomeworkManagement:RemoveQuestion";
        public const string Homework_Resend = "HomeworkManagement:Resend";
        public const string Homework_Archive = "HomeworkManagement:Archive";
        public const string Homework_Delete = "HomeworkManagement:Delete";

        // ===============================
        // PerformanceDashboard
        // ===============================
        public const string PerformanceDashboard_Read = "PerformanceDashboard:Read";
        public const string PerformanceDashboard_BatchDetails = "PerformanceDashboard:BatchDetails";
        public const string PerformanceDashboard_ExamReport = "PerformanceDashboard:ExamReport";
        public const string PerformanceDashboard_ExamAnalytics = "PerformanceDashboard:ExamAnalytics";

        // ===============================
        // PerformanceExamReports
        // ===============================
        public const string PerformanceExamReports_Read = "PerformanceExamReports:Read";
        public const string PerformanceExamReports_StudentReport = "PerformanceExamReports:StudentReport";
        public const string PerformanceExamReports_BatchReport = "PerformanceExamReports:BatchReport";

        // ===============================
        // PerformanceIndicatorExams
        // ===============================
        public const string PerformanceIndicatorExams_Read = "PerformanceIndicatorExams:Read";
        public const string PerformanceIndicatorExams_Add = "PerformanceIndicatorExams:Add";
        public const string PerformanceIndicatorExams_Edit = "PerformanceIndicatorExams:Edit";
        public const string PerformanceIndicatorExams_CreateFromProfessionalModel = "PerformanceIndicatorExams:CreateFromProfessionalModel";
        public const string PerformanceIndicatorExams_ConfirmSend = "PerformanceIndicatorExams:ConfirmSend";
        public const string PerformanceIndicatorExams_Results = "PerformanceIndicatorExams:Results";
        public const string PerformanceIndicatorExams_Delete = "PerformanceIndicatorExams:Delete";
        public const string PerformanceIndicatorExams_Archive = "PerformanceIndicatorExams:Archive";

        // ===============================
        // ProfessionalModels
        // ===============================
        public const string ProfessionalModels_Read = "ProfessionalModels:Read";
        public const string ProfessionalModels_CreateModel = "ProfessionalModels:CreateModel";
        public const string ProfessionalModels_EditModel = "ProfessionalModels:EditModel";
        public const string ProfessionalModels_DeleteModel = "ProfessionalModels:DeleteModel";
        public const string ProfessionalModels_ApproveModel = "ProfessionalModels:ApproveModel";
        public const string ProfessionalModels_RejectModel = "ProfessionalModels:RejectModel";
        public const string ProfessionalModels_Visibility = "ProfessionalModels:Visibility";
        public const string ProfessionalModels_Duplicate = "ProfessionalModels:Duplicate";

        // ===============================
        // Settings
        // ===============================
        public const string Settings_EditSettings = "Settings:EditSettings";

        // ===============================
        // Attendance
        // ===============================
        public const string Attendance_Read = "Attendance:Read";
        public const string Attendance_Mark = "Attendance:Mark";
        public const string Attendance_Students = "Attendance:Students";
        public const string Attendance_Archive = "Attendance:Archive";

        // ===============================
        // EnhancementSkills
        // ===============================
        public const string EnhancementSkills_Read = "EnhancementSkills:Read";
        public const string EnhancementSkills_Create = "EnhancementSkills:Create";
        public const string EnhancementSkills_Edit = "EnhancementSkills:Edit";
        public const string EnhancementSkills_Send = "EnhancementSkills:Send";
        public const string EnhancementSkills_Resend = "EnhancementSkills:Resend";
        public const string EnhancementSkills_Delete = "EnhancementSkills:Delete";
        public const string EnhancementSkills_Archive = "EnhancementSkills:Archive";

        // ===============================
        // Lectures
        // ===============================
        public const string Lectures_Read = "Lectures:Read";
        public const string Lectures_Create = "Lectures:Create";
        public const string Lectures_Edit = "Lectures:Edit";
        public const string Lectures_Delete = "Lectures:Delete";

        // ===============================
        // VerbalPassages
        // ===============================
        public const string VerbalPassages_Read = "VerbalPassages:Read";
        public const string VerbalPassages_Create = "VerbalPassages:Create";
        public const string VerbalPassages_Edit = "VerbalPassages:Edit";
        public const string VerbalPassages_Delete = "VerbalPassages:Delete";

        // ===============================
        // Batches
        // ===============================
        public const string Batches_Read = "Batches:Read";
        public const string Batches_Details = "Batches:Details";
        public const string Batches_Create = "Batches:Create";
        public const string Batches_Edit = "Batches:Edit";
        public const string Batches_Delete = "Batches:Delete";
        public const string Batches_Archive = "Batches:Archive";
        public const string Batches_Students = "Batches:Students";
        public const string Batches_Send = "Batches:Send";

        // ===============================
        // IntegrityViolations (Translation Guard — QG-E)
        // ===============================
        public const string IntegrityViolations_Read = "IntegrityViolations:Read";
        public const string IntegrityViolations_Resolve = "IntegrityViolations:Resolve";
    }
}
