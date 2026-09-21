using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class StudentAIAnalysisResult
    {
        public string AssessedLevel { get; set; } = string.Empty;    // تقييم الأداء
        public int RiskScore { get; set; }                            // درجة الخطر (1-ممتاز, 2-متوسط, 3-ضعيف)
        public List<string> Recommendations { get; set; } = new();   // التوصيات
    }
}
