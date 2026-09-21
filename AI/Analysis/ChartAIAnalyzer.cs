using Microsoft.ML;
using QdratNew.ViewModels.Shared;
using QdratNew.AI.MLModels.Charts;
using System;
using System.IO;
using System.Linq;
using QdratNew.ViewModels.AI;

namespace QdratNew.AI.Analysis
{
    public static class ChartAIAnalyzer
    {
        private static readonly string modelPath = Path.Combine("MLModels", "Charts", "ChartAnalyzerModel.zip");
        private static readonly MLContext mlContext = new MLContext();
        private static PredictionEngine<ChartPredictionInput, ChartPredictionOutput>? _predictionEngine;

        static ChartAIAnalyzer()
        {
            Console.WriteLine("📦 محاولة تحميل نموذج الذكاء الاصطناعي من المسار:");
            Console.WriteLine(modelPath);

            if (File.Exists(modelPath))
            {
                try
                {
                    var model = mlContext.Model.Load(modelPath, out _);
                    _predictionEngine = mlContext.Model.CreatePredictionEngine<ChartPredictionInput, ChartPredictionOutput>(model);
                    Console.WriteLine("✅ تم تحميل نموذج الرسم البياني بنجاح.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ فشل تحميل النموذج: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("❌ لم يتم العثور على النموذج في: " + modelPath);
            }
        }

        public static ChartAIInsightViewModel Analyze(ChartAnalysisInputViewModel chart)
        {
            if (_predictionEngine == null)
            {
                return new ChartAIInsightViewModel
                {
                    Comment = "⚠ لا يمكن تحليل الرسم البياني لعدم توفر نموذج مدرب.",
                    Recommendation = "يرجى التأكد من وجود ملف نموذج ML.NET في المسار الصحيح."
                };
            }

            if (chart.DataPoints == null || !chart.DataPoints.Any())
            {
                return new ChartAIInsightViewModel
                {
                    Comment = "⚠ لا توجد بيانات لتحليل هذا الرسم البياني.",
                    Recommendation = "يرجى إدخال نقاط بيانات كافية لتحليل الرسم."
                };
            }

            var values = chart.DataPoints.Select(p => p.Value).ToList();
            var average = values.Average();
            var stdDev = Math.Sqrt(values.Average(v => Math.Pow((double)(v - average), 2)));

            var input = new ChartPredictionInput
            {
                AverageValue = (float)average,
                MaxValue = (float)values.Max(),
                MinValue = (float)values.Min(),
                StdDeviation = (float)stdDev,
                Count = (float)values.Count
            };

            try
            {
                var prediction = _predictionEngine.Predict(input);

                return new ChartAIInsightViewModel
                {
                    Comment = $"📊 التصنيف المتوقع لهذا الرسم البياني: {prediction.Prediction}",
                    Recommendation = prediction.Prediction switch
                    {
                        "Stable" => "✅ الأداء مستقر، يُوصى بالاستمرار بنفس الأسلوب.",
                        "Risk" => "⚠ تذبذب ملحوظ، يُوصى بتحليل الأسباب واتخاذ إجراءات احترازية.",
                        "Growth" => "📈 الاتجاه تصاعدي، استثمر هذا النمو في تعميم الأسلوب.",
                        "Decline" => "📉 هناك تراجع، راجع المؤشرات وتدخل مبكرًا.",
                        _ => "❔ لم يتمكن النظام من تصنيف الرسم بشكل دقيق. راجع البيانات أو أعد تدريب النموذج."
                    }
                };
            }
            catch (Exception ex)
            {
                return new ChartAIInsightViewModel
                {
                    Comment = "❌ فشل التحليل بسبب خطأ في النموذج.",
                    Recommendation = "الرسالة: " + ex.Message
                };
            }
        }
    }
}
