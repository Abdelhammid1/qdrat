namespace QdratNew.Services.Exams.Generators
{
    public interface IExamAttendanceTracker
    {
        /// <summary>
        /// يسجل حضور الطالب بمجرد فتح الاختبار.
        /// </summary>
        Task MarkStudentPresentAsync(int examAssignmentId, int studentId);

        /// <summary>
        /// يغلق الاختبارات المنتهية تلقائيًا (لجميع الطلاب).
        /// </summary>
        Task AutoCompleteExpiredExamsAsync();

        /// <summary>
        /// يسجل أن الطالب أنهى الاختبار يدويًا (حتى لو لم يضغط على إنهاء).
        /// </summary>
        Task MarkExamAsCompletedAsync(int examAssignmentId, int studentId);
    }

}
