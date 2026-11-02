namespace QdratNew.Services.Exams.Placement
{
    // PPL-S2: Success/Info/Error تقابل TempData، وNotFound/BadRequest تقابل نتيجة HTTP في الـ Controller
    public enum PlacementOperationOutcome { Success, Info, Error, NotFound, BadRequest }

    public sealed class PlacementOperationResult
    {
        public PlacementOperationOutcome Outcome { get; init; }
        public bool Succeeded => Outcome == PlacementOperationOutcome.Success;
        public string Message { get; init; } = string.Empty;
        public int? ExamId { get; init; }
        public int? BatchId { get; init; }

        public static PlacementOperationResult Ok(string msg, int? examId = null, int? batchId = null)
            => new() { Outcome = PlacementOperationOutcome.Success, Message = msg, ExamId = examId, BatchId = batchId };

        public static PlacementOperationResult Info(string msg)
            => new() { Outcome = PlacementOperationOutcome.Info, Message = msg };

        public static PlacementOperationResult Fail(string msg)
            => new() { Outcome = PlacementOperationOutcome.Error, Message = msg };

        public static PlacementOperationResult NotFound()
            => new() { Outcome = PlacementOperationOutcome.NotFound, Message = "غير مسموح" };

        public static PlacementOperationResult BadRequest(string msg)
            => new() { Outcome = PlacementOperationOutcome.BadRequest, Message = msg };
    }
}
