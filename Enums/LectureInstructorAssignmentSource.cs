namespace QdratNew.Enums
{
    // Auto = مشتق تلقائيًا من ربط InstructorCurriculumBatch ويخضع للمزامنة التلقائية.
    // Manual = عيّنه الأدمن يدويًا من شاشة المحاضرات، ولا تلمسه المزامنة التلقائية أبدًا.
    public enum LectureInstructorAssignmentSource
    {
        Auto = 0,
        Manual = 1
    }
}
