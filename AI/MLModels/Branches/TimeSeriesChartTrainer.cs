using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.IO;

namespace QdratNew.AI.MLModels.Branches
{
    public class ChartTimeSeriesData
    {
        [LoadColumn(0)]
        public float Value { get; set; }

        [LoadColumn(1)]
        public string Date { get; set; }  // Format: yyyy-MM
        public string Month { get; set; } // ✅ هذه السطر هو المهم

    }

    public class ChartTimeSeriesForecast
    {
        public float[] ForecastedValues { get; set; }
        public float[] LowerBound { get; set; }
        public float[] UpperBound { get; set; }
    }

    public static class TimeSeriesChartTrainer
    {
        public static void TrainAndSave(string modelPath, List<ChartTimeSeriesData> data)
        {
            var mlContext = new MLContext();

            var trainingData = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(ChartTimeSeriesForecast.ForecastedValues),
                inputColumnName: nameof(ChartTimeSeriesData.Value),
                windowSize: 4,
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

}
