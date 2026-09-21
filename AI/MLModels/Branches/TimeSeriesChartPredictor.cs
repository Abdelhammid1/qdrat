using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using QdratNew.ViewModels.Shared;

namespace QdratNew.AI.MLModels.Branches
{
    public static class TimeSeriesChartPredictor
    {
        public static List<TimeSeriesForecastPoint> Predict(string modelPath, int forecastHorizon)
        {
            var mlContext = new MLContext();

            if (!File.Exists(modelPath))
                return new List<TimeSeriesForecastPoint>();

            // ✅ تحميل النموذج المدرب
            ITransformer trainedModel = mlContext.Model.Load(modelPath, out var modelInputSchema);

            var predictionEngine = trainedModel.CreateTimeSeriesEngine<ChartTimeSeriesData, ChartTimeSeriesForecast>(mlContext);

            var forecast = predictionEngine.Predict();

            var results = new List<TimeSeriesForecastPoint>();
            for (int i = 0; i < forecastHorizon; i++)
            {
                results.Add(new TimeSeriesForecastPoint
                {
                    MonthLabel = $"الشهر القادم {i + 1}",
                    PredictedValue = forecast.ForecastedValues[i],
                    LowerBound = forecast.LowerBound[i],
                    UpperBound = forecast.UpperBound[i]
                });
            }

            return results;
        }
    }

    public class TimeSeriesForecastPoint
    {
        public string MonthLabel { get; set; }
        public float PredictedValue { get; set; }
        public float LowerBound { get; set; }
        public float UpperBound { get; set; }

        public string Date { get; set; } // بصيغة yyyy-MM
        public float ForecastedValue { get; set; }
       
    }
}
