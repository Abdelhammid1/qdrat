using System.Collections.Generic;

namespace QdratNew.ViewModels.Course
{
    public class CourseStatisticsViewModel
    {
        public int TotalCourses { get; set; }
        public int ActiveCourses { get; set; }
        public int InactiveCourses { get; set; }
        public string MostPopularCourse { get; set; }
        public Dictionary<int, float> CoursePredictions { get; set; } // ✅ التوقعات المستقبلية لكل شهر

        public List<CourseTrendViewModel> CourseTrends { get; set; } // ✅ إضافة بيانات الاتجاهات المستقبلية
    }

    public class CourseTrendViewModel
    {
        public int CourseId { get; set; }
        public int Month { get; set; }
        public int Registrations { get; set; }
    }
}
