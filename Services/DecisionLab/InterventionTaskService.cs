using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public class InterventionTaskService : IInterventionTaskService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = null,
            WriteIndented = true
        };

        private readonly ApplicationDbContext _context;
        private readonly IDecisionLabAnalysisService _analysisService;

        public InterventionTaskService(
            ApplicationDbContext context,
            IDecisionLabAnalysisService analysisService)
        {
            _context = context;
            _analysisService = analysisService;
        }

        public async Task<CreateInterventionTaskViewModel?> BuildCreateModelAsync(int decisionRecommendationId)
        {
            var recommendation = await _context.DecisionRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == decisionRecommendationId);

            if (recommendation == null || recommendation.Status != "Approved" || !recommendation.BatchId.HasValue)
            {
                return null;
            }

            var existingTaskId = await GetTaskIdForRecommendationAsync(decisionRecommendationId);
            if (existingTaskId.HasValue)
            {
                return null;
            }

            var batchInfo = await LoadBatchInfoAsync(recommendation.BatchId.Value);
            if (batchInfo == null)
            {
                return null;
            }

            var curriculumTitle = await LoadCurriculumTitleAsync(recommendation.CurriculumId);
            var targetLessonsJson = ExtractJsonArray(recommendation.RecommendationJson, "RelatedWeakLessons");
            var targetQuestionsJson = ExtractJsonArray(recommendation.RecommendationJson, "RelatedHighRiskQuestions");

            var model = new CreateInterventionTaskViewModel
            {
                DecisionRecommendationId = recommendation.Id,
                BatchId = recommendation.BatchId.Value,
                CourseId = batchInfo.CourseId,
                CurriculumId = recommendation.CurriculumId,
                BatchName = batchInfo.BatchName,
                CourseName = batchInfo.CourseName,
                CurriculumTitle = curriculumTitle,
                RecommendationType = recommendation.RecommendationType,
                RecommendationSummary = recommendation.Summary,
                RecommendationReason = recommendation.Reason,
                RecommendationStatus = recommendation.Status,
                Title = BuildDefaultTitle(recommendation),
                Description = recommendation.Reason,
                TaskType = MapTaskType(recommendation.RecommendationType),
                DeliveryMode = "InLecture",
                TargetType = "WholeBatch",
                Priority = MapPriority(recommendation.RecommendationJson),
                OwnerInstructions = BuildDefaultOwnerInstructions(recommendation),
                TargetLessonsJson = targetLessonsJson,
                TargetQuestionsJson = targetQuestionsJson,
                BeforeSnapshotJson = recommendation.InputSnapshotJson
            };

            await FillLookupsAsync(model);
            SetRecommendedLecture(model);
            return model;
        }

        public async Task<CreateInterventionTaskViewModel?> RebuildCreateModelAsync(CreateInterventionTaskViewModel model)
        {
            var recommendation = await _context.DecisionRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == model.DecisionRecommendationId);

            if (recommendation == null)
            {
                return null;
            }

            var batchInfo = await LoadBatchInfoAsync(model.BatchId);
            model.BatchName = batchInfo?.BatchName ?? string.Empty;
            model.CourseName = batchInfo?.CourseName ?? string.Empty;
            model.CurriculumTitle = await LoadCurriculumTitleAsync(model.CurriculumId);
            model.RecommendationType = recommendation.RecommendationType;
            model.RecommendationSummary = recommendation.Summary;
            model.RecommendationReason = recommendation.Reason;
            model.RecommendationStatus = recommendation.Status;
            model.BeforeSnapshotJson ??= recommendation.InputSnapshotJson;
            model.TargetLessonsJson ??= ExtractJsonArray(recommendation.RecommendationJson, "RelatedWeakLessons");
            model.TargetQuestionsJson ??= ExtractJsonArray(recommendation.RecommendationJson, "RelatedHighRiskQuestions");

            await FillLookupsAsync(model);
            return model;
        }

        public async Task<int> CreateAsync(CreateInterventionTaskViewModel model, string createdByUserId)
        {
            if (string.IsNullOrWhiteSpace(createdByUserId))
            {
                throw new ArgumentException("Created user id is required.", nameof(createdByUserId));
            }

            var recommendation = await _context.DecisionRecommendations
                .FirstOrDefaultAsync(item => item.Id == model.DecisionRecommendationId);

            if (recommendation == null)
            {
                throw new InvalidOperationException("DecisionLab recommendation was not found.");
            }

            if (recommendation.Status != "Approved")
            {
                throw new InvalidOperationException("Intervention tasks can only be created from approved DecisionLab recommendations.");
            }

            var existingTask = await _context.InterventionTasks
                .AsNoTracking()
                .Where(item => item.DecisionRecommendationId == recommendation.Id)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();

            if (existingTask.HasValue)
            {
                return existingTask.Value;
            }

            var now = DateTime.Now;
            var task = new InterventionTask
            {
                DecisionRecommendationId = recommendation.Id,
                BatchId = model.BatchId,
                CourseId = model.CourseId,
                CurriculumId = model.CurriculumId,
                InstructorId = model.InstructorId,
                LectureId = model.LectureId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim() ?? string.Empty,
                TaskType = model.TaskType,
                DeliveryMode = model.DeliveryMode,
                TargetType = model.TargetType,
                Priority = model.Priority,
                Status = "Assigned",
                DueDate = model.DueDate,
                OwnerInstructions = model.OwnerInstructions.Trim(),
                TargetLessonsJson = model.TargetLessonsJson,
                TargetQuestionsJson = model.TargetQuestionsJson,
                BeforeSnapshotJson = model.BeforeSnapshotJson,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                AssignedAt = now
            };

            _context.InterventionTasks.Add(task);
            await _context.SaveChangesAsync();

            return task.Id;
        }

        public async Task<InterventionTaskListViewModel> GetListAsync()
        {
            var tasks = await (
                from task in _context.InterventionTasks.AsNoTracking()
                join batch in _context.Batches.AsNoTracking()
                    on task.BatchId equals batch.Id
                join instructor in _context.Instructors.AsNoTracking()
                    on task.InstructorId equals instructor.Id into instructorJoin
                from instructor in instructorJoin.DefaultIfEmpty()
                join lecture in _context.Lecture.AsNoTracking()
                    on task.LectureId equals lecture.Id into lectureJoin
                from lecture in lectureJoin.DefaultIfEmpty()
                orderby task.CreatedAt descending
                select new InterventionTaskListItemViewModel
                {
                    Id = task.Id,
                    BatchId = task.BatchId,
                    CurriculumId = task.CurriculumId,
                    Title = task.Title,
                    BatchName = batch.Name,
                    InstructorName = instructor != null ? instructor.FullName : string.Empty,
                    LectureTitle = lecture != null ? lecture.Title : string.Empty,
                    TaskType = task.TaskType,
                    DeliveryMode = task.DeliveryMode,
                    Priority = task.Priority,
                    Status = task.Status,
                    DueDate = task.DueDate,
                    CreatedAt = task.CreatedAt
                })
                .ToListAsync();

            var today = DateTime.Today;
            foreach (var task in tasks)
            {
                task.IsOverdue = task.DueDate.HasValue
                    && task.DueDate.Value.Date < today
                    && task.Status != "CompletedByInstructor";
            }

            return new InterventionTaskListViewModel
            {
                TotalTasks = tasks.Count,
                AssignedTasks = tasks.Count(task => task.Status == "Assigned"),
                InProgressTasks = tasks.Count(task => task.Status == "InProgress"),
                CompletedByInstructorTasks = tasks.Count(task => task.Status == "CompletedByInstructor"),
                OverdueTasks = tasks.Count(task => task.IsOverdue),
                Tasks = tasks
            };
        }

        public async Task<InterventionTaskDetailsViewModel?> GetDetailsAsync(int id)
        {
            var model = await (
                from task in _context.InterventionTasks.AsNoTracking()
                join recommendation in _context.DecisionRecommendations.AsNoTracking()
                    on task.DecisionRecommendationId equals recommendation.Id into recommendationJoin
                from recommendation in recommendationJoin.DefaultIfEmpty()
                join batch in _context.Batches.AsNoTracking()
                    on task.BatchId equals batch.Id
                join course in _context.Courses.AsNoTracking()
                    on task.CourseId equals course.Id into courseJoin
                from course in courseJoin.DefaultIfEmpty()
                join curriculum in _context.Curriculums.AsNoTracking()
                    on task.CurriculumId equals curriculum.Id into curriculumJoin
                from curriculum in curriculumJoin.DefaultIfEmpty()
                join instructor in _context.Instructors.AsNoTracking()
                    on task.InstructorId equals instructor.Id into instructorJoin
                from instructor in instructorJoin.DefaultIfEmpty()
                join lecture in _context.Lecture.AsNoTracking()
                    on task.LectureId equals lecture.Id into lectureJoin
                from lecture in lectureJoin.DefaultIfEmpty()
                where task.Id == id
                select new InterventionTaskDetailsViewModel
                {
                    Id = task.Id,
                    DecisionRecommendationId = task.DecisionRecommendationId,
                    BatchId = task.BatchId,
                    CourseId = task.CourseId,
                    CurriculumId = task.CurriculumId,
                    SectionId = task.SectionId,
                    LessonId = task.LessonId,
                    InstructorId = task.InstructorId,
                    LectureId = task.LectureId,
                    BatchName = batch.Name,
                    CourseName = course != null ? course.Name : string.Empty,
                    CurriculumTitle = curriculum != null ? curriculum.Title : string.Empty,
                    InstructorName = instructor != null ? instructor.FullName : string.Empty,
                    LectureTitle = lecture != null ? lecture.Title : string.Empty,
                    Title = task.Title,
                    Description = task.Description,
                    TaskType = task.TaskType,
                    DeliveryMode = task.DeliveryMode,
                    TargetType = task.TargetType,
                    Priority = task.Priority,
                    Status = task.Status,
                    DueDate = task.DueDate,
                    OwnerInstructions = task.OwnerInstructions,
                    TargetLessonsJson = task.TargetLessonsJson,
                    TargetQuestionsJson = task.TargetQuestionsJson,
                    RecommendationSummary = recommendation != null ? recommendation.Summary : string.Empty,
                    RecommendationReason = recommendation != null ? recommendation.Reason : string.Empty,
                    RecommendationType = recommendation != null ? recommendation.RecommendationType : string.Empty,
                    RecommendationStatus = recommendation != null ? recommendation.Status : string.Empty,
                    CreatedByUserId = task.CreatedByUserId,
                    CreatedAt = task.CreatedAt,
                    AssignedAt = task.AssignedAt,
                    StartedAt = task.StartedAt,
                    CompletedAt = task.CompletedAt,
                    AfterSnapshotJson = task.AfterSnapshotJson
                })
                .FirstOrDefaultAsync();

            if (model != null)
            {
                model.SnapshotComparison = await BuildSnapshotComparisonAsync(model);
            }

            return model;
        }

        private async Task<SnapshotComparisonVM?> BuildSnapshotComparisonAsync(InterventionTaskDetailsViewModel task)
        {
            if (!task.DecisionRecommendationId.HasValue)
            {
                return null;
            }

            var rec = await _context.DecisionRecommendations
                .AsNoTracking()
                .Where(r => r.Id == task.DecisionRecommendationId.Value)
                .Select(r => new
                {
                    r.InputSnapshotJson,
                    r.CreatedAt,
                    r.BatchId,
                    r.CurriculumId
                })
                .FirstOrDefaultAsync();

            if (rec == null || string.IsNullOrWhiteSpace(rec.InputSnapshotJson) || !rec.BatchId.HasValue)
            {
                return null;
            }

            double? snapshotAvg      = null;
            double? snapshotAtRisk   = null;

            try
            {
                using var doc = JsonDocument.Parse(rec.InputSnapshotJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("AverageExamScore", out var avgProp)
                    && avgProp.ValueKind == JsonValueKind.Number)
                {
                    snapshotAvg = avgProp.GetDouble();
                }

                if (root.TryGetProperty("WeakLessonsCount", out var weakProp)
                    && weakProp.ValueKind == JsonValueKind.Number)
                {
                    snapshotAtRisk = weakProp.GetDouble();
                }
            }
            catch
            {
                return null;
            }

            double? currentAvg    = null;
            double? currentAtRisk = null;

            try
            {
                var current = await _analysisService.AnalyzeBatchAsync(
                    rec.BatchId.Value,
                    rec.CurriculumId,
                    fromDate: null,
                    toDate: null);

                if (current != null)
                {
                    currentAvg    = current.AverageExamScore;
                    currentAtRisk = current.WeakLessonsCount;
                }
            }
            catch
            {
                // تعذّر جلب التحليل الحالي — نعرض ما لدينا من الـ snapshot فقط
            }

            return new SnapshotComparisonVM
            {
                SnapshotAvgExamScore = snapshotAvg,
                CurrentAvgExamScore  = currentAvg,
                SnapshotAtRiskCount  = snapshotAtRisk,
                CurrentAtRiskCount   = currentAtRisk,
                DaysSinceDecision    = (int)(DateTime.Now - rec.CreatedAt).TotalDays
            };
        }

        public async Task<int?> GetTaskIdForRecommendationAsync(int decisionRecommendationId)
        {
            return await _context.InterventionTasks
                .AsNoTracking()
                .Where(item => item.DecisionRecommendationId == decisionRecommendationId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();
        }

        private async Task FillLookupsAsync(CreateInterventionTaskViewModel model)
        {
            model.TaskTypeOptions = BuildOptions(
                ("InstructorReExplanation", "إعادة شرح من المدرب"),
                ("QuestionReview", "مراجعة أسئلة"),
                ("HomeworkReinforcement", "تعزيز بالواجب"),
                ("ShortRemedialExam", "اختبار علاجي قصير"),
                ("AttendanceFollowUp", "متابعة حضور"),
                ("StudentFollowUp", "متابعة طلاب"),
                ("MixedIntervention", "تدخل مركب"));

            model.DeliveryModeOptions = BuildOptions(
                ("InLecture", "داخل المحاضرة"),
                ("Online", "عن بعد"),
                ("Hybrid", "هجين"));

            model.TargetTypeOptions = BuildOptions(
                ("WholeBatch", "الدفعة كاملة"),
                ("SelectedStudents", "طلاب محددون"),
                ("InstructorOnly", "المدرب فقط"));

            model.PriorityOptions = BuildOptions(
                ("Low", "منخفضة"),
                ("Normal", "عادية"),
                ("High", "عالية"),
                ("Critical", "حرجة"));

            model.InstructorOptions = await _context.Instructors
                .AsNoTracking()
                .Where(instructor => instructor.IsActive && !instructor.IsDeleted)
                .OrderBy(instructor => instructor.FullName)
                .Select(instructor => new SelectListItem
                {
                    Value = instructor.Id.ToString(),
                    Text = instructor.FullName
                })
                .ToListAsync();

            var lecturesQuery = _context.Lecture
                .AsNoTracking()
                .Where(lecture => lecture.BatchId == model.BatchId);

            if (model.InstructorId.HasValue)
            {
                lecturesQuery = lecturesQuery.Where(lecture => lecture.InstructorId == model.InstructorId.Value);
            }

            model.LectureOptions = await lecturesQuery
                .OrderBy(lecture => lecture.Date)
                .Select(lecture => new SelectListItem
                {
                    Value = lecture.Id.ToString(),
                    Text = lecture.Title + " - " + lecture.Date.ToString("yyyy/MM/dd")
                })
                .ToListAsync();

            model.LectureMap = await LoadLectureMapAsync(model);
        }

        private async Task<IReadOnlyList<InterventionTaskLectureMapItemViewModel>> LoadLectureMapAsync(
            CreateInterventionTaskViewModel model)
        {
            var query =
                from lecture in _context.Lecture.AsNoTracking()
                join instructor in _context.Instructors.AsNoTracking()
                    on lecture.InstructorId equals instructor.Id into instructorJoin
                from instructor in instructorJoin.DefaultIfEmpty()
                join section in _context.Sections.AsNoTracking()
                    on lecture.SectionId equals section.Id
                join curriculum in _context.Curriculums.AsNoTracking()
                    on section.CurriculumId equals curriculum.Id into curriculumJoin
                from curriculum in curriculumJoin.DefaultIfEmpty()
                where lecture.BatchId == model.BatchId
                      && (!model.CourseId.HasValue || lecture.CourseId == model.CourseId.Value)
                      && (!model.CurriculumId.HasValue || section.CurriculumId == model.CurriculumId.Value)
                select new
                {
                    lecture.Id,
                    lecture.Title,
                    lecture.Date,
                    InstructorName = instructor != null ? instructor.FullName : string.Empty,
                    CurriculumTitle = curriculum != null ? curriculum.Title : string.Empty
                };

            var today = DateTime.Today;
            var lectures = await query
                .OrderBy(lecture => lecture.Date)
                .ToListAsync();

            var recommendedLectureId = lectures
                .Where(lecture => lecture.Date.Date >= today)
                .Select(lecture => (int?)lecture.Id)
                .FirstOrDefault();

            model.RecommendedNextLectureId = recommendedLectureId;

            return lectures
                .Select(lecture => new InterventionTaskLectureMapItemViewModel
                {
                    LectureId = lecture.Id,
                    Title = lecture.Title,
                    Date = lecture.Date,
                    InstructorName = lecture.InstructorName,
                    CurriculumTitle = lecture.CurriculumTitle,
                    StatusText = ResolveLectureStatus(lecture.Date, today),
                    IsRecommendedNextLecture = recommendedLectureId.HasValue
                        && lecture.Id == recommendedLectureId.Value
                })
                .ToList();
        }

        private static void SetRecommendedLecture(CreateInterventionTaskViewModel model)
        {
            if (!model.LectureId.HasValue && model.RecommendedNextLectureId.HasValue)
            {
                model.LectureId = model.RecommendedNextLectureId.Value;
            }
        }

        private static string ResolveLectureStatus(DateTime lectureDate, DateTime today)
        {
            if (lectureDate.Date < today)
            {
                return "سابقة";
            }

            if (lectureDate.Date == today)
            {
                return "اليوم";
            }

            return "قادمة";
        }

        private async Task<BatchInfo?> LoadBatchInfoAsync(int batchId)
        {
            return await (
                from batch in _context.Batches.AsNoTracking()
                join course in _context.Courses.AsNoTracking()
                    on batch.CourseId equals course.Id
                where batch.Id == batchId
                select new BatchInfo
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name,
                    CourseId = course.Id,
                    CourseName = course.Name
                })
                .FirstOrDefaultAsync();
        }

        private async Task<string> LoadCurriculumTitleAsync(int? curriculumId)
        {
            if (!curriculumId.HasValue)
            {
                return string.Empty;
            }

            return await _context.Curriculums
                .AsNoTracking()
                .Where(curriculum => curriculum.Id == curriculumId.Value)
                .Select(curriculum => curriculum.Title)
                .FirstOrDefaultAsync()
                ?? string.Empty;
        }

        private static IReadOnlyList<SelectListItem> BuildOptions(params (string Value, string Text)[] options)
        {
            return options
                .Select(option => new SelectListItem
                {
                    Value = option.Value,
                    Text = option.Text
                })
                .ToList();
        }

        private static string BuildDefaultTitle(DecisionRecommendation recommendation)
        {
            return string.IsNullOrWhiteSpace(recommendation.Summary)
                ? "مهمة تدخل تعليمية من DecisionLab"
                : recommendation.Summary;
        }

        private static string BuildDefaultOwnerInstructions(DecisionRecommendation recommendation)
        {
            return string.IsNullOrWhiteSpace(recommendation.Reason)
                ? "راجع التوصية وحدد التدخل التعليمي المناسب للدفعة."
                : recommendation.Reason;
        }

        private static string MapTaskType(string recommendationType)
        {
            return recommendationType switch
            {
                "ShortRemedialExam" => "ShortRemedialExam",
                "AttendanceFollowUp" => "AttendanceFollowUp",
                "HomeworkSupport" => "HomeworkReinforcement",
                "InstructorGuidance" => "InstructorReExplanation",
                "MixedIntervention" => "MixedIntervention",
                _ => "InstructorReExplanation"
            };
        }

        private static string MapPriority(string recommendationJson)
        {
            var priority = ExtractString(recommendationJson, "Priority");

            return priority switch
            {
                "عاجل" => "Critical",
                "مرتفع" => "High",
                "متوسط" => "Normal",
                "منخفض" => "Low",
                _ => "Normal"
            };
        }

        private static string? ExtractJsonArray(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.Array
                    && property.GetArrayLength() > 0)
                {
                    return JsonSerializer.Serialize(property, JsonOptions);
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        private static string ExtractString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private sealed class BatchInfo
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = string.Empty;
            public int CourseId { get; set; }
            public string CourseName { get; set; } = string.Empty;
        }
    }
}
