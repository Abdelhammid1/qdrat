using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.HomeworkEngine.Assignment;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.InterventionTasks;
using System.Text.Json;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorInterventionTasksController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHomeworkEngineAssignmentService _assignmentService;

        public InstructorInterventionTasksController(
            ApplicationDbContext context,
            IHomeworkEngineAssignmentService assignmentService,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context           = context;
            _assignmentService = assignmentService;
        }

        // GET: /Instructors/InstructorInterventionTasks
        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var statusOrder = new Dictionary<string, int>
            {
                ["Assigned"] = 0,
                ["InProgress"] = 1,
                ["NeedsFollowUp"] = 2,
                ["CompletedByInstructor"] = 3
            };

            var tasks = await (
                from t in _context.InterventionTasks.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on t.BatchId equals b.Id
                where t.InstructorId == instructorId
                select new InstructorTaskListItemVM
                {
                    Id = t.Id,
                    Title = t.Title,
                    TaskType = t.TaskType,
                    Priority = t.Priority,
                    Status = t.Status,
                    BatchName = b.Name,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt
                }
            ).ToListAsync();

            tasks = tasks
                .OrderBy(t => statusOrder.TryGetValue(t.Status, out var o) ? o : 99)
                .ThenByDescending(t => t.CreatedAt)
                .ToList();

            return View(tasks);
        }

        // GET: /Instructors/InstructorInterventionTasks/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var vm = await (
                from t in _context.InterventionTasks.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on t.BatchId equals b.Id
                join cur in _context.Curriculums.AsNoTracking() on t.CurriculumId equals cur.Id into curJoin
                from cur in curJoin.DefaultIfEmpty()
                join lec in _context.Lecture.AsNoTracking() on t.LectureId equals lec.Id into lecJoin
                from lec in lecJoin.DefaultIfEmpty()
                where t.Id == id && t.InstructorId == instructorId
                select new InstructorTaskDetailsVM
                {
                    Id = t.Id,
                    BatchId = t.BatchId,
                    Title = t.Title,
                    Description = t.Description,
                    TaskType = t.TaskType,
                    DeliveryMode = t.DeliveryMode,
                    TargetType = t.TargetType,
                    Priority = t.Priority,
                    Status = t.Status,
                    OwnerInstructions = t.OwnerInstructions,
                    InstructorExecutionNotes = t.InstructorExecutionNotes,
                    TargetLessonsJson = t.TargetLessonsJson,
                    TargetQuestionsJson = t.TargetQuestionsJson,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    AssignedAt = t.AssignedAt,
                    StartedAt = t.StartedAt,
                    CompletedAt = t.CompletedAt,
                    BatchName = b.Name,
                    CurriculumTitle = cur != null ? cur.Title : null,
                    LectureTitle = lec != null ? lec.Title : null
                }
            ).FirstOrDefaultAsync();

            if (vm == null)
                return NotFound();

            // جلب الطلاب المسجلين في الدفعة
            var students = await (
                from enrollment in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                join student in _context.Set<Student>().AsNoTracking()
                    on enrollment.StudentID equals student.StudentID
                where enrollment.BatchId == vm.BatchId
                orderby student.FullName
                select new { student.StudentID, student.FullName }
            ).ToListAsync();

            vm.TargetStudentsJson = JsonSerializer.Serialize(students);

            return View(vm);
        }

        // POST: /Instructors/InstructorInterventionTasks/StartTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTask(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var task = await _context.InterventionTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.InstructorId == instructorId);

            if (task == null)
                return NotFound();

            if (task.Status != "Assigned")
                return RedirectToAction(nameof(Details), new { id });

            task.Status = "InProgress";
            task.StartedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Instructors/InstructorInterventionTasks/GetQuestionStudents
        [HttpGet]
        public async Task<IActionResult> GetQuestionStudents(int taskId, Guid questionId)
        {
            if (taskId <= 0)
                return Json(new { error = "taskId مطلوب" });

            if (questionId == Guid.Empty)
                return Json(new { error = "questionId غير صالح" });

            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var task = await _context.InterventionTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId && t.InstructorId == instructorId)
                .Select(t => new { t.BatchId })
                .FirstOrDefaultAsync();

            if (task == null)
                return NotFound();

            try
            {
            // خطوة 1: معرّفات طلاب الدفعة
            var batchStudentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(sbe => sbe.BatchId == task.BatchId)
                .Select(sbe => sbe.StudentID)
                .Distinct()
                .ToListAsync();

            if (!batchStudentIds.Any())
                return Json(Array.Empty<object>());

            // خطوة 2: المحاولات الخاطئة لهذا السؤال من طلاب الدفعة فقط
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(qa => qa.QuestionId == questionId
                          && !qa.IsCorrect
                          && batchStudentIds.Contains(qa.StudentId))
                .Select(qa => new
                {
                    qa.StudentId,
                    TimeSecs = (double?)qa.TimeTakenSeconds ?? 0
                })
                .ToListAsync();

            if (!attempts.Any())
                return Json(Array.Empty<object>());

            // خطوة 3: تجميع في الذاكرة
            var grouped = attempts
                .GroupBy(a => a.StudentId)
                .Select(g => new
                {
                    StudentId     = g.Key,
                    AttemptCount  = g.Count(),
                    TotalTimeSecs = (int)g.Sum(a => a.TimeSecs)
                })
                .OrderByDescending(g => g.AttemptCount)
                .Take(50)
                .ToList();

            // خطوة 4: أسماء الطلاب
            var studentIds = grouped.Select(g => g.StudentId).ToList();
            var nameMap = await _context.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.StudentID))
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            var nameLookup = nameMap.ToDictionary(s => s.StudentID, s => s.FullName ?? "طالب");

            var result = grouped.Select(g => new
            {
                StudentName   = nameLookup.GetValueOrDefault(g.StudentId, "طالب"),
                g.AttemptCount,
                g.TotalTimeSecs
            }).ToList();

            return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // POST: /Instructors/InstructorInterventionTasks/CompleteTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(int id, string? executionNotes)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var task = await _context.InterventionTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.InstructorId == instructorId);

            if (task == null)
                return NotFound();

            if (task.Status != "Assigned" && task.Status != "InProgress")
                return RedirectToAction(nameof(Details), new { id });

            task.Status = "CompletedByInstructor";
            task.CompletedAt = DateTime.Now;
            task.CompletedByUserId = _userManager.GetUserId(User);
            task.InstructorExecutionNotes = executionNotes?.Trim();

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Instructors/InstructorInterventionTasks/GetQuestionStudents?taskId=X&questionId=GUID
        [HttpGet]
        public async Task<IActionResult> GetQuestionStudents(int taskId, string questionId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var task = await _context.InterventionTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId && t.InstructorId == instructorId);

            if (task == null)
                return NotFound();

            if (!Guid.TryParse(questionId, out var questionGuid))
                return BadRequest(new { error = "معرف السؤال غير صالح" });

            var studentData = await (
                from qa in _context.QuestionAttemptNew.AsNoTracking()
                join student in _context.Set<Student>().AsNoTracking()
                    on qa.StudentId equals student.StudentID
                join sbe in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                    on student.StudentID equals sbe.StudentID
                where qa.QuestionId == questionGuid
                   && sbe.BatchId == task.BatchId
                   && !qa.IsCorrect
                group qa by new { qa.StudentId, student.FullName } into g
                select new
                {
                    StudentName  = g.Key.FullName,
                    AttemptCount = g.Count(),
                    TotalTimeSecs = (int)g.Sum(x => x.TimeTakenSeconds)
                }
            ).OrderByDescending(x => x.AttemptCount)
             .ThenByDescending(x => x.TotalTimeSecs)
             .ToListAsync();

            return Json(studentData);
        }

        // GET: /Instructors/InstructorInterventionTasks/CompleteWithHomework/5
        [HttpGet]
        public async Task<IActionResult> CompleteWithHomework(
            int id,
            string? homeworkQuestionIds = null,
            string? lectureQuestionIds = null,
            string? homeworkLessonIds = null,
            string? lectureLessonIds = null)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0) return Forbid();

            var row = await (
                from t in _context.InterventionTasks.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on t.BatchId equals b.Id
                where t.Id == id && t.InstructorId == instructorId
                select new { t, BatchName = b.Name }
            ).FirstOrDefaultAsync();

            if (row == null) return NotFound();

            if (row.t.Status != "Assigned" && row.t.Status != "InProgress")
                return RedirectToAction(nameof(Details), new { id });

            var allQuestions = ParseTaskQuestions(row.t.TargetQuestionsJson);
            var allLessons = ParseTaskLessons(row.t.TargetLessonsJson);
            var selectedHomeworkQuestionIds = ParseQuestionIdSet(homeworkQuestionIds);
            var selectedLectureQuestionIds = ParseQuestionIdSet(lectureQuestionIds);
            var selectedHomeworkLessonIds = ParseIntSet(homeworkLessonIds);
            var selectedLectureLessonIds = ParseIntSet(lectureLessonIds);
            var allowedQuestionIds = allQuestions
                .Select(q => q.QuestionId)
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowedLessonIds = allLessons
                .Select(l => l.LessonId)
                .Where(l => l > 0)
                .ToHashSet();

            selectedHomeworkQuestionIds.IntersectWith(allowedQuestionIds);
            selectedLectureQuestionIds.IntersectWith(allowedQuestionIds);
            selectedHomeworkLessonIds.IntersectWith(allowedLessonIds);
            selectedLectureLessonIds.IntersectWith(allowedLessonIds);

            var hasCarriedSelection = selectedHomeworkQuestionIds.Any()
                || selectedLectureQuestionIds.Any()
                || selectedHomeworkLessonIds.Any()
                || selectedLectureLessonIds.Any();

            var visibleQuestions = hasCarriedSelection
                ? allQuestions.Where(q => selectedHomeworkQuestionIds.Contains(q.QuestionId)).ToList()
                : allQuestions;

            var visibleLessons = hasCarriedSelection
                ? allLessons
                    .Where(l => selectedHomeworkLessonIds.Contains(l.LessonId)
                             || selectedLectureLessonIds.Contains(l.LessonId))
                    .ToList()
                : allLessons;

            var students = await (
                from e in _context.Set<StudentBatchEnrollment>().AsNoTracking()
                join s in _context.Set<Student>().AsNoTracking() on e.StudentID equals s.StudentID
                where e.BatchId == row.t.BatchId
                orderby s.FullName
                select new BatchStudentItem { StudentId = s.StudentID, FullName = s.FullName ?? "طالب" }
            ).ToListAsync();

            var vm = new CompleteWithHomeworkVM
            {
                TaskId                 = id,
                TaskTitle              = row.t.Title,
                BatchId                = row.t.BatchId,
                BatchName              = row.BatchName,
                OwnerInstructions      = row.t.OwnerInstructions,
                SuggestedHomeworkTitle = $"واجب تدخل: {row.t.Title}",
                LectureId              = row.t.LectureId,
                HomeworkEndAt          = row.t.DueDate?.AddDays(1) ?? DateTime.Now.AddDays(3),
                Questions              = visibleQuestions,
                Lessons                = visibleLessons,
                Students               = students,
                HasCarriedSelection    = hasCarriedSelection,
                SelectedHomeworkQuestionIds = selectedHomeworkQuestionIds.ToList(),
                SelectedLectureQuestionIds = selectedLectureQuestionIds.ToList(),
                SelectedHomeworkLessonIds = selectedHomeworkLessonIds.ToList(),
                SelectedLectureLessonIds = selectedLectureLessonIds.ToList()
            };

            return View(vm);
        }

        // POST: /Instructors/InstructorInterventionTasks/SubmitCompletion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitCompletion(SubmitCompletionInputVM model)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0) return Forbid();

            var task = await _context.InterventionTasks
                .FirstOrDefaultAsync(t => t.Id == model.TaskId && t.InstructorId == instructorId);

            if (task == null) return NotFound();

            if (task.Status != "Assigned" && task.Status != "InProgress")
                return RedirectToAction(nameof(Details), new { id = model.TaskId });

            // Parse questions/lessons from task for storing titles in snapshot
            var allQuestions = ParseTaskQuestions(task.TargetQuestionsJson);
            var allLessons   = ParseTaskLessons(task.TargetLessonsJson);

            var allowedHomeworkQuestionIds = allQuestions
                .Select(q => q.QuestionId)
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var hwQIds = (model.HomeworkQuestionIds ?? new List<string>())
                .Where(s => Guid.TryParse(s, out _))
                .Select(Guid.Parse)
                .Where(q => allowedHomeworkQuestionIds.Contains(q.ToString()))
                .Distinct()
                .ToList();
            var hwQIdSet = hwQIds
                .Select(q => q.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var lecQIdSet = (model.LectureQuestionIds ?? new List<string>())
                .Where(s => Guid.TryParse(s, out _))
                .Select(s => Guid.Parse(s).ToString())
                .Where(allowedHomeworkQuestionIds.Contains)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowedSnapshotLessonIds = allLessons
                .Select(l => l.LessonId)
                .Where(l => l > 0)
                .ToHashSet();
            var hwLessonIdSet = (model.HomeworkLessonIds ?? new List<int>())
                .Where(allowedSnapshotLessonIds.Contains)
                .ToHashSet();
            var lecLessonIdSet = (model.LectureLessonIds ?? new List<int>())
                .Where(allowedSnapshotLessonIds.Contains)
                .ToHashSet();

            int homeworkSetId = 0;
            var finalStudentIds = new List<int>();

            if (hwQIds.Any())
            {
                if (model.SendTo == "Students" && model.SelectedStudentIds?.Any() == true)
                {
                    finalStudentIds = model.SelectedStudentIds.Distinct().ToList();
                }
                else
                {
                    finalStudentIds = await _context.Set<StudentBatchEnrollment>()
                        .AsNoTracking()
                        .Where(sbe => sbe.BatchId == task.BatchId)
                        .Select(sbe => sbe.StudentID)
                        .Distinct()
                        .ToListAsync();
                }

                if (finalStudentIds.Any())
                {
                    var userId = _userManager.GetUserId(User) ?? string.Empty;
                    var endAt  = model.HomeworkEndAt == default ? DateTime.Now.AddDays(3) : model.HomeworkEndAt;
                    homeworkSetId = _assignmentService.SendDirectToStudents(
                        model.HomeworkTitle ?? task.Title,
                        task.BatchId, task.LectureId,
                        DateTime.Now, endAt,
                        hwQIds, finalStudentIds, userId);
                }
            }

            // Build AfterSnapshot — include titles for admin display
            var hwQDetails  = allQuestions.Where(q => hwQIdSet.Contains(q.QuestionId))
                                          .Select(q => new { q.QuestionId, q.QuestionTitle, q.LessonTitle, q.RiskLevel }).ToList();
            var lecQDetails = allQuestions.Where(q => lecQIdSet.Contains(q.QuestionId))
                                          .Select(q => new { q.QuestionId, q.QuestionTitle, q.LessonTitle, q.RiskLevel }).ToList();
            var hwLDetails  = allLessons.Where(l => hwLessonIdSet.Contains(l.LessonId))
                                        .Select(l => new { l.LessonId, l.LessonTitle, l.SectionTitle }).ToList();
            var lecLDetails = allLessons.Where(l => lecLessonIdSet.Contains(l.LessonId))
                                        .Select(l => new { l.LessonId, l.LessonTitle, l.SectionTitle }).ToList();

            var afterSnapshot = new
            {
                CompletedAt           = DateTime.Now,
                ExecutionNotes        = model.ExecutionNotes?.Trim(),
                HomeworkTitle         = model.HomeworkTitle,
                HomeworkSetId         = homeworkSetId,
                SentTo                = model.SendTo,
                SolvedInLecture       = lecQDetails,
                SentAsHomework        = hwQDetails,
                LectureLessons        = lecLDetails,
                HomeworkLessons       = hwLDetails,
                StudentIds            = finalStudentIds,
                StudentCount          = finalStudentIds.Count
            };

            task.Status                  = "CompletedByInstructor";
            task.CompletedAt             = DateTime.Now;
            task.CompletedByUserId       = _userManager.GetUserId(User);
            task.InstructorExecutionNotes = model.ExecutionNotes?.Trim();
            task.AfterSnapshotJson       = JsonSerializer.Serialize(afterSnapshot,
                new JsonSerializerOptions { PropertyNamingPolicy = null });

            await _context.SaveChangesAsync();

            TempData["CompletionSuccess"] = "تم إتمام المهمة وإرسال الواجب بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.TaskId });
        }

        // ── Helpers ────────────────────────────────────────────────
        private static HashSet<string> ParseQuestionIdSet(string? csv)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv))
                return result;

            foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(token, out var id))
                    result.Add(id.ToString());
            }

            return result;
        }

        private static HashSet<int> ParseIntSet(string? csv)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(csv))
                return result;

            foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out var id) && id > 0)
                    result.Add(id);
            }

            return result;
        }

        private static List<TaskQuestionItem> ParseTaskQuestions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<TaskQuestionItem>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = new List<TaskQuestionItem>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string qid = el.TryGetProperty("QuestionId", out var v1) ? v1.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(qid)) continue;
                    result.Add(new TaskQuestionItem
                    {
                        QuestionId           = qid,
                        QuestionTitle        = el.TryGetProperty("QuestionTitle",        out var v2) ? v2.GetString() ?? "" : "",
                        ReferenceNumber      = el.TryGetProperty("ReferenceNumber",      out var v3) ? v3.GetString() ?? "" : "",
                        LessonId             = el.TryGetProperty("LessonId",             out var v4) && v4.ValueKind == JsonValueKind.Number ? v4.GetInt32() : 0,
                        LessonTitle          = el.TryGetProperty("LessonTitle",          out var v5) ? v5.GetString() ?? "" : "",
                        SectionTitle         = el.TryGetProperty("SectionTitle",         out var v6) ? v6.GetString() ?? "" : "",
                        ErrorPercentage      = el.TryGetProperty("ErrorPercentage",      out var v7) && v7.ValueKind == JsonValueKind.Number ? v7.GetDouble() : 0,
                        RiskLevel            = el.TryGetProperty("RiskLevel",            out var v8) ? v8.GetString() ?? "" : "",
                        AffectedStudentsCount= el.TryGetProperty("AffectedStudentsCount",out var v9) && v9.ValueKind == JsonValueKind.Number ? v9.GetInt32() : 0
                    });
                }
                return result;
            }
            catch { return new List<TaskQuestionItem>(); }
        }

        private static List<TaskLessonItem> ParseTaskLessons(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<TaskLessonItem>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = new List<TaskLessonItem>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    int lid = el.TryGetProperty("LessonId", out var v1) && v1.ValueKind == JsonValueKind.Number ? v1.GetInt32() : 0;
                    if (lid <= 0) continue;
                    result.Add(new TaskLessonItem
                    {
                        LessonId              = lid,
                        LessonTitle           = el.TryGetProperty("LessonTitle",           out var v2) ? v2.GetString() ?? "" : "",
                        SectionTitle          = el.TryGetProperty("SectionTitle",          out var v3) ? v3.GetString() ?? "" : "",
                        ErrorPercentage       = el.TryGetProperty("ErrorPercentage",       out var v4) && v4.ValueKind == JsonValueKind.Number ? v4.GetDouble() : 0,
                        RiskLevel             = el.TryGetProperty("RiskLevel",             out var v5) ? v5.GetString() ?? "" : "",
                        AffectedStudentsCount = el.TryGetProperty("AffectedStudentsCount", out var v6) && v6.ValueKind == JsonValueKind.Number ? v6.GetInt32() : 0,
                        RelatedQuestionsCount = el.TryGetProperty("RelatedQuestionsCount", out var v7) && v7.ValueKind == JsonValueKind.Number ? v7.GetInt32() : 0
                    });
                }
                return result;
            }
            catch { return new List<TaskLessonItem>(); }
        }

        // POST: /Instructors/InstructorInterventionTasks/ArchiveTasks
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveTasks(List<int> selectedIds)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            if (selectedIds == null || !selectedIds.Any())
                return RedirectToAction(nameof(Index));

            var tasks = await _context.InterventionTasks
                .Where(t => selectedIds.Contains(t.Id)
                         && t.InstructorId == instructorId
                         && t.Status == "CompletedByInstructor")
                .ToListAsync();

            foreach (var task in tasks)
                task.Status = "ArchivedByInstructor";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: /Instructors/InstructorInterventionTasks/Archive
        public async Task<IActionResult> Archive()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId <= 0)
                return Forbid();

            var tasks = await (
                from t in _context.InterventionTasks.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on t.BatchId equals b.Id
                where t.InstructorId == instructorId && t.Status == "ArchivedByInstructor"
                select new InstructorTaskListItemVM
                {
                    Id = t.Id,
                    Title = t.Title,
                    TaskType = t.TaskType,
                    Priority = t.Priority,
                    Status = t.Status,
                    BatchName = b.Name,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt
                }
            ).OrderByDescending(t => t.CreatedAt).ToListAsync();

            return View(tasks);
        }
    }
}
