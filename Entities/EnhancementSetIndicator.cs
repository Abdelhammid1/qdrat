namespace QdratNew.Entities
{
    public class EnhancementSetIndicator
    {
        public int Id { get; set; }

        public int EnhancementSkillSetId { get; set; }
        public EnhancementSkillSet EnhancementSkillSet { get; set; }

        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }

        // عدد الأسئلة التي تُسحب من هذا المؤشر لكل طالب
        public int QuestionCount { get; set; }
    }
}
