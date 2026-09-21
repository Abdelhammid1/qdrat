using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QdratNew.AI.Trainers.Charts
{
    public static class TimeSeriesChartTrainer
    {
        public static void TrainAndSave(string modelPath, List<ChartTimeSeriesData> data)
        {
            var mlContext = new MLContext();

            var trainingData = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(ChartTimeSeriesForecast.ForecastedValues),
                inputColumnName: nameof(ChartTimeSeriesData.Value),
                windowSize: 7,
                seriesLength: data.Count,
                trainSize: data.Count,
                horizon: 6,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBound",
                confidenceUpperBoundColumn: "UpperBound");

            var model = pipeline.Fit(trainingData);

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            mlContext.Model.Save(model, trainingData.Schema, modelPath);
        }
    }

    public class ChartTimeSeriesData
    {
        [LoadColumn(0)]
        public float Value { get; set; }

        [LoadColumn(1)]
        public string Date { get; set; }  // Format: yyyy-MM
    }

    public class ChartTimeSeriesForecast
    {
        public float[] ForecastedValues { get; set; }
    }
}
