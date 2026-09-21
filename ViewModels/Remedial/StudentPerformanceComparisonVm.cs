namespace QdratNew.ViewModels.Remedial
{
    public class StudentPerformanceComparisonVm
    {
        public string StudentName { get; set; }
        public string CurriculumTitle { get; set; }

        public List<StudentSectionPerformanceVm> Sections { get; set; } = new();
        public int StudentId { get; internal set; }
        public int CurriculumId { get; internal set; }
        public double AverageHomeworkScore { get; internal set; }
        public double AverageExamScore { get; internal set; }
        public double AverageIndicatorScore { get; internal set; }
        public List<WeakSectionVm> WeakSections { get; internal set; }
        public string SectionTitle { get; internal set; }
        public string Recommendation { get; internal set; }
        public string LessonTitle { get; internal set; }
    }

    /// <summary>
    /// تحليل أداء الطالب داخل محور واحد (Section)
    /// </summary>
    public class StudentSectionPerformanceVm
    {
        /// <summary>
        /// اسم المحور
        /// </summary>
        public string SectionTitle { get; set; }

        /// <summary>
        /// متوسط درجة الطالب في الواجبات الخاصة بالمحور
        /// </summary>
        public double HomeworkAverage { get; set; }

        /// <summary>
        /// متوسط درجة الطالب في الاختبارات العامة للمحور
        /// </summary>
        public double ExamAverage { get; set; }

        /// <summary>
        /// نتيجة الطالب في اختبار مؤشر الأداء للمحور
        /// </summary>
        public double IndicatorScore { get; set; }

        /// <summary>
        /// الاتجاه العام (تحسّن، تراجع، ثبات)
        /// </summary>
        public string Trend { get; set; }

        /// <summary>
        /// التوصية الذكية بناءً على المقارنة
        /// </summary>
        public string Recommendation { get; set; }

        /// <summary>
        /// اللون المقترح للعرض في الجدول (اختياري)
        /// </summary>
        public string DisplayColor
        {
            get
            {
                if (IndicatorScore < 50)
                    return "table-danger";   // 🔴 ضعف شديد
                if (IndicatorScore < 70)
                    return "table-warning";  // 🟠 ضعف متوسط
                if (IndicatorScore < 85)
                    return "table-info";     // 🟡 أداء مقبول
                return "table-success";       // 🟢 ممتاز
            }
        }
    }




}
