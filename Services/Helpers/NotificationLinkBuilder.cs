namespace QdratNew.Services.Helpers
{
    public static class NotificationLinkBuilder
    {
        // ==========================
        // 🔵 روابط الواجبات
        // ==========================

        public static string HomeworkStart(int homeworkSetId)
            => $"/Students/StudentHomeworkDashboard/Start?id={homeworkSetId}";

        public static string HomeworkResult(int homeworkSetId)
            => $"/Students/StudentHomeworkDashboard/Result?id={homeworkSetId}";

        public static string HomeworkDashboard()
            => "/Students/StudentHomeworkDashboard/Dashboard";


        // ==========================
        // 🔴 روابط الاختبارات
        // ==========================

        public static string ExamStart(int examAssignmentId)
            => $"/Students/Exams/StartExam?examAssignmentId={examAssignmentId}";

        public static string IndividualExamStart(int examAssignmentId)
            => $"/Students/Exams/StartIndividualExam?examAssignmentId={examAssignmentId}";

        public static string ExamResult(int examAssignmentId)
            => $"/Students/Exams/ExamResult?examAssignmentId={examAssignmentId}";

        public static string ExamDashboard()
            => "/Students/Dashboard/Index";
    }
}
