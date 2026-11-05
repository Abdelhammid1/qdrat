using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.ProfessionalModels;

namespace QdratNew.Services.PartnerDispatch
{
    public class PartnerModelDispatchService : IPartnerModelDispatchService
    {
        private readonly IProfessionalModelSendService _sendService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PartnerModelDispatchService> _logger;

        public PartnerModelDispatchService(
            IProfessionalModelSendService sendService,
            ApplicationDbContext context,
            ILogger<PartnerModelDispatchService> logger)
        {
            _sendService = sendService;
            _context = context;
            _logger = logger;
        }

        public Task<PartnerDispatchOutcome> SendHomeworkAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendModelHomeworkRequest request, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return DispatchAsync(
                PartnerDispatchKind.Homework,
                ProfessionalModelType.Homework,
                targets,
                actor,
                request.ConfirmWarnings,
                batchIds => new SendModelHomeworkRequest
                {
                    ModelId = request.ModelId,
                    BatchIds = batchIds,
                    Title = request.Title,
                    StartAtLocal = request.StartAtLocal,
                    EndAtLocal = request.EndAtLocal,
                    ConfirmWarnings = request.ConfirmWarnings
                },
                (scope, perPartner) => _sendService.SendHomeworkAsync(scope, perPartner, ct),
                ct);
        }

        public Task<PartnerDispatchOutcome> SendExamAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendModelExamRequest request, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return DispatchAsync(
                PartnerDispatchKind.Exam,
                ProfessionalModelType.Exam,
                targets,
                actor,
                request.ConfirmWarnings,
                batchIds => new SendModelExamRequest
                {
                    ModelId = request.ModelId,
                    BatchIds = batchIds,
                    Title = request.Title,
                    StartAtLocal = request.StartAtLocal,
                    EndAtLocal = request.EndAtLocal,
                    DurationMinutes = request.DurationMinutes,
                    IsInLab = request.IsInLab,
                    RandomizeQuestions = request.RandomizeQuestions,
                    ConfirmWarnings = request.ConfirmWarnings
                },
                (scope, perPartner) => _sendService.SendExamAsync(scope, perPartner, ct),
                ct);
        }

        private async Task<PartnerDispatchOutcome> DispatchAsync<TRequest>(
            PartnerDispatchKind kind,
            ProfessionalModelType modelType,
            IReadOnlyList<PartnerDispatchTarget> targets,
            DispatchActor actor,
            bool confirmed,
            Func<List<int>, TRequest> requestFor,
            Func<ModelSendScope, TRequest, Task<ModelSendResult>> sendAsync,
            CancellationToken ct)
            where TRequest : SendFromModelRequestBase
        {
            ArgumentNullException.ThrowIfNull(targets);
            ArgumentNullException.ThrowIfNull(actor);

            var outcome = new PartnerDispatchOutcome { DispatchGroupId = Guid.NewGuid(), Kind = kind };

            if (targets.Count == 0)
            {
                outcome.ValidationError = "⚠️ اختر مدرسة شريكة ودفعة واحدة على الأقل.";
                return outcome;
            }

            var sends = targets
                .Select(target => (
                    Target: target,
                    Scope: ModelSendScope.AdminForPartner(target.PartnerId, actor.UserId, actor.UserName, target.ContractOverridden, target.OverrideReason, outcome.DispatchGroupId),
                    Request: requestFor(target.BatchIds)))
                .ToList();

            // المواعيد والمدة لا تختلف بين المدارس، فلا تتكرر الرسالة نفسها لكل مدرسة
            outcome.ValidationError = _sendService.ValidateSchedule(sends[0].Scope, sends[0].Request);
            if (outcome.ValidationError != null)
                return outcome;

            // تحذيرات التكرار تُعرض مرة واحدة قبل أي كتابة
            var warnings = new List<BatchWarningVm>();
            ModelSendPreview? firstPreview = null;

            foreach (var (target, scope, request) in sends)
            {
                var preview = await _sendService.PreviewAsync(scope, modelType, request, ct);
                if (preview == null)
                {
                    outcome.IsNotFound = true;
                    return outcome;
                }

                firstPreview ??= preview;

                // خطأ دفعات أو أسئلة يخص هذه المدرسة ويظهر في نتيجتها عند الإرسال
                if (preview.ValidationError != null)
                    continue;

                warnings.AddRange(preview.Batches.Select(batch => new BatchWarningVm
                {
                    BatchId = batch.BatchId,
                    Name = $"{target.PartnerName} — {batch.Name}",
                    StudentCount = batch.StudentCount,
                    RepeatedQuestionCount = batch.RepeatedQuestionCount,
                    AlreadySentFromModel = batch.AlreadySentFromModel
                }));
            }

            if (!confirmed && warnings.Any(b => b.RepeatedQuestionCount > 0 || b.AlreadySentFromModel))
            {
                outcome.RequiresConfirmation = true;
                outcome.Preview = new ModelSendPreview
                {
                    ModelId = firstPreview!.ModelId,
                    ModelTitle = firstPreview.ModelTitle,
                    ModelType = firstPreview.ModelType,
                    Batches = warnings
                };
                return outcome;
            }

            foreach (var (target, scope, request) in sends)
                outcome.Results.Add(await SendToPartnerAsync(target, scope, request, sendAsync));

            return outcome;
        }

        private async Task<PartnerDispatchPartnerResult> SendToPartnerAsync<TRequest>(
            PartnerDispatchTarget target,
            ModelSendScope scope,
            TRequest request,
            Func<ModelSendScope, TRequest, Task<ModelSendResult>> sendAsync)
            where TRequest : SendFromModelRequestBase
        {
            var result = new PartnerDispatchPartnerResult
            {
                PartnerId = target.PartnerId,
                PartnerName = target.PartnerName,
                ContractOverridden = target.ContractOverridden
            };

            try
            {
                var sent = await sendAsync(scope, request);
                result.Succeeded = sent.Succeeded;
                result.Message = sent.IsNotFound ? "⚠️ النموذج لم يعد متاحًا." : sent.Message;
                result.BatchesSent = sent.BatchesSent;
                result.StudentsSent = sent.StudentsSent;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "APD dispatch failed for partner {PartnerId}, group {DispatchGroupId}", target.PartnerId, scope.DispatchGroupId);

                result.Succeeded = false;
                result.Message = "⚠️ تعذر الإرسال لهذه المدرسة، ولم يُحفظ لها أي شيء.";
            }

            return result;
        }
    }
}
