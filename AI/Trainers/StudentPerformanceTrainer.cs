using Microsoft.ML;
using QdratNew.AI.MLModels.Students;
using QdratNew.Data;
using System.IO;
using System.Linq;

namespace QdratNew.AI.Trainers
{
    public class StudentPerformanceTrainer
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private readonly string _modelPath = "MLModels/studentPerformanceModel.zip";

        public StudentPerformanceTrainer()
        {
            _mlContext = new MLContext();
        }

        // 📌 تدريب باستخدام DbContext مباشرة
        public void TrainAndSaveModel(ApplicationDbContext context)
        {
            // 1️⃣ تجهيز البيانات من الجداول
            var trainingData = (
                from sp in context.StudentPerformances
                join plan in context.StudyPlans on sp.StudentID equals plan.StudentID into spPlan
                from plan in spPlan.DefaultIfEmpty()
                join prog in context.StudentProgress on sp.StudentID equals prog.StudentID into spProg
                from prog in spProg.DefaultIfEmpty()
                select new StudentPerformanceData
                {
                    PreviousScore = (float)sp.Score,
                    StudyHours = plan != null ? (float)plan.HoursPerWeek : 10f,
                    ExercisesCompleted = prog != null ? (float)prog.CompletedExercises : 20f,
                    AttendanceCount = prog != null ? (float)prog.CompletedLessons : 15f,
                    EngagementRate = prog != null ? (float)prog.ProgressPercentage : 50f,
                    Score = (float)sp.Score // 🟢 الـ Label
                }
            ).ToList();

            // 2️⃣ تحميل البيانات لـ IDataView
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // 3️⃣ بناء الـ Pipeline
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                    nameof(StudentPerformanceData.PreviousScore),
                    nameof(StudentPerformanceData.StudyHours),
                    nameof(StudentPerformanceData.ExercisesCompleted),
                    nameof(StudentPerformanceData.AttendanceCount),
                    nameof(StudentPerformanceData.EngagementRate)
                )
                .Append(_mlContext.Regression.Trainers.FastTree(labelColumnName: "Score"));

            // 4️⃣ تدريب الموديل
            _model = pipeline.Fit(dataView);

            // 5️⃣ حفظ الموديل
            _mlContext.Model.Save(_model, dataView.Schema, _modelPath);
        }

        // ✅ نفس PredictScore اللي كتبناه قبل كده
        public float PredictScore(float previousScore, float studyHours, float exercises, float attendance, float engagement)
        {
            if (_model == null)
            {
                if (File.Exists(_modelPath))
                {
                    DataViewSchema schema;
                    _model = _mlContext.Model.Load(_modelPath, out schema);
                }
                else
                {
                    throw new FileNotFoundException("⚠️ الموديل غير مدرب. لازم تستدعي TrainAndSaveModel أولاً.");
                }
            }

            var predEngine = _mlContext.Model.CreatePredictionEngine<StudentPerformanceData, StudentPerformancePrediction>(_model);

            var input = new StudentPerformanceData
            {
                PreviousScore = previousScore,
                StudyHours = studyHours,
                ExercisesCompleted = exercises,
                AttendanceCount = attendance,
                EngagementRate = engagement
            };

            return predEngine.Predict(input).PredictedScore;
        }
    }
}
