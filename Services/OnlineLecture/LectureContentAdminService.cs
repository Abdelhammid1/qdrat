using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S2.2 — منطق الأدمن للمحتوى التعليمي أونلاين. كل الاستعلامات Projection + AsNoTracking للقراءة،
    /// والكتابة SaveChanges واحدة لكل عملية (لا حلقات استعلام).
    /// </summary>
    public sealed partial class LectureContentAdminService : ILectureContentAdminService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<LectureContentAdminService> _logger;
        private readonly IPartnerEducationalContentPolicy _partnerPolicy; // OLC-S4.1 (R16)
        private readonly QdratNew.Services.Interfaces.ITimeZoneService _tz; // OLC-S7.4: موعد المحاضرة (توقيت السعودية) ← UTC

        public LectureContentAdminService(
            ApplicationDbContext db,
            ILogger<LectureContentAdminService> logger,
            IPartnerEducationalContentPolicy? partnerPolicy = null,
            QdratNew.Services.Interfaces.ITimeZoneService? timeZone = null)
        {
            _db = db;
            _logger = logger;
            _partnerPolicy = partnerPolicy ?? new PartnerEducationalContentPolicy(db);
            _tz = timeZone ?? new QdratNew.Services.Implementations.TimeZoneService();
        }

        // ═══════════════════════════ القراءة ═══════════════════════════

        public async Task<LectureContentEditVm?> GetForLectureAsync(int lectureId, CancellationToken ct = default)
        {
            var lecture = await _db.Lecture
                .AsNoTracking()
                .Where(l => l.Id == lectureId)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Date,
                    l.ScheduledTime,
                    l.BatchId,
                    BatchName = l.Batch.Name,
                    BatchIsOnline = l.Batch.IsOnlineLearning,
                    l.SectionId,
                    SectionTitle = l.Section.Title,
                    CurriculumId = (int?)l.Section.CurriculumId
                })
                .FirstOrDefaultAsync(ct);

            if (lecture is null)
                return null;

            var vm = new LectureContentEditVm
            {
                LectureId = lecture.Id,
                LectureTitle = lecture.Title ?? string.Empty,
                LectureDate = lecture.Date,
                ScheduledTime = lecture.ScheduledTime,
                BatchId = lecture.BatchId,
                BatchName = lecture.BatchName ?? string.Empty,
                BatchIsOnline = lecture.BatchIsOnline,
                SectionTitle = string.IsNullOrWhiteSpace(lecture.SectionTitle) ? "غير محدد" : lecture.SectionTitle,
                SectionId = lecture.SectionId,
                CreateInput = new LectureContentCreateVm
                {
                    Title = Truncate(lecture.Title, 200),
                    ContentMode = LectureContentMode.FullVideo
                }
            };

            var content = await _db.LectureContents
                .AsNoTracking()
                .Where(c => c.LectureId == lectureId)
                .Select(c => new
                {
                    c.Id,
                    c.ContentMode,
                    c.Status,
                    c.Title,
                    c.Description,
                    c.VideoProvider,
                    c.VideoUrl,
                    c.VideoExternalId,
                    c.DurationSeconds,
                    c.ThumbnailUrl,
                    c.PublishedAt,
                    c.PublishedByUserId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    ActiveItemsCount = c.Items.Count()
                })
                .FirstOrDefaultAsync(ct);

            if (content is null)
                return vm;

            vm.ContentId = content.Id;
            vm.ContentMode = content.ContentMode;
            vm.Status = content.Status;
            vm.PublishedAt = content.PublishedAt;
            vm.CreatedAt = content.CreatedAt;
            vm.UpdatedAt = content.UpdatedAt;
            vm.ActiveItemsCount = content.ActiveItemsCount;
            vm.VideoProvider = content.VideoProvider;
            vm.EmbedPreviewUrl = content.ContentMode == LectureContentMode.FullVideo
                ? LectureVideoUrlParser.TryBuildEmbedUrl(content.VideoProvider, content.VideoExternalId)
                : null;
            vm.Input = new LectureContentUpdateVm
            {
                Title = content.Title,
                Description = content.Description,
                ContentMode = content.ContentMode,
                VideoUrl = content.VideoUrl,
                DurationText = LectureTimeFormat.Format(content.DurationSeconds),
                ThumbnailUrl = content.ThumbnailUrl
            };
            vm.DurationSeconds = content.DurationSeconds;
            vm.PublishedByUserId = content.PublishedByUserId;

            // OLC-S3: الفصول/قائمة الفيديوهات + موانع النشر وتنبيهاته
            await FillEditorAsync(vm, lecture.CurriculumId, content.VideoExternalId, ct);
            return vm;
        }

        public async Task<int?> GetLectureIdOfContentAsync(int contentId, CancellationToken ct = default)
            => await _db.LectureContents
                .AsNoTracking()
                .Where(c => c.Id == contentId)
                .Select(c => (int?)c.LectureId)
                .FirstOrDefaultAsync(ct);

        public async Task<BatchOnlineLearningSummaryVm?> GetBatchOnlineSummaryAsync(int batchId, CancellationToken ct = default)
        {
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId && !b.IsDeleted)
                .Select(b => new { b.Id, b.IsOnlineLearning, b.OnlineLearningEnabledAt, b.IsArchived })
                .FirstOrDefaultAsync(ct);

            if (batch is null)
                return null;

            var lecturesCount = await _db.Lecture.AsNoTracking().CountAsync(l => l.BatchId == batchId, ct);

            var statusCounts = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == batchId
                    group c by c.Status into g
                    select new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            // OLC-S4.2: دفعة شريك ⇒ حالة البند في عقده (R16) تُعرض في البطاقة
            var partner = await _partnerPolicy.CheckBatchAsync(batchId, ct);

            return new BatchOnlineLearningSummaryVm
            {
                BatchId = batch.Id,
                IsOnlineLearning = batch.IsOnlineLearning,
                OnlineLearningEnabledAt = batch.OnlineLearningEnabledAt,
                IsArchived = batch.IsArchived,
                LecturesCount = lecturesCount,
                ContentsCount = statusCounts.Sum(x => x.Count),
                PublishedCount = statusCounts.Where(x => x.Status == LectureContentStatus.Published).Sum(x => x.Count),
                PartnerId = partner?.PartnerId,
                PartnerName = partner?.PartnerName,
                PartnerContractState = partner is { IsPartnerBatch: true } ? partner.State : null,
                PartnerBlockReason = partner is { CanActivate: false } ? _partnerPolicy.DescribeBlock(partner) : null
            };
        }

        public async Task<IReadOnlyDictionary<int, LectureContentStatus>> GetStatusesForBatchAsync(int batchId, CancellationToken ct = default)
        {
            var rows = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == batchId
                    select new { c.LectureId, c.Status })
                .ToListAsync(ct);

            return rows.ToDictionary(x => x.LectureId, x => x.Status);
        }

        public Task<bool> LectureHasContentAsync(int lectureId, CancellationToken ct = default)
            => _db.LectureContents.IgnoreQueryFilters().AsNoTracking().AnyAsync(c => c.LectureId == lectureId, ct);

        // ═══════════════════════════ الدفعة الأونلاين (S2.1) ═══════════════════════════

        public async Task<LectureContentResult> SetBatchOnlineLearningAsync(int batchId, bool enable, CancellationToken ct = default)
        {
            var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted, ct);
            if (batch is null)
                return LectureContentResult.Missing("الدفعة غير موجودة.");

            if (batch.IsOnlineLearning == enable)
                return LectureContentResult.Ok(enable
                    ? "التعليم أونلاين مفعّل بالفعل لهذه الدفعة."
                    : "التعليم أونلاين غير مفعّل لهذه الدفعة أصلًا.");

            if (enable)
            {
                if (batch.IsArchived)
                    return LectureContentResult.Fail("لا يمكن تفعيل التعليم أونلاين لدفعة مؤرشفة. استرجعها من الأرشيف أولًا.");

                // R16 (OLC-S4.2): دفعة شريك تُفعَّل فقط بعقد نشط الآن يشمل بند «المحتوى التعليمي» — بلا تجاوز
                var check = await _partnerPolicy.CheckBatchAsync(batchId, ct);
                if (check is null)
                    return LectureContentResult.Missing("الدفعة غير موجودة.");
                if (!check.CanActivate)
                    return LectureContentResult.Fail(_partnerPolicy.DescribeBlock(check));

                batch.IsOnlineLearning = true;
                batch.OnlineLearningEnabledAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                OnlineLearningMenuCacheSignal.Invalidate();   // OLC-S5.1: رابط القائمة يظهر فورًا لطلاب الدفعة
                return LectureContentResult.Ok("تم تفعيل التعليم أونلاين للدفعة. يمكنك الآن إضافة المحتوى من صفحة كل محاضرة.");
            }

            // الإلغاء: يُخفي القسم عن طلاب الدفعة فقط — المحتوى والتقدّم يبقيان كما هما (OnlineLearningEnabledAt = آخر تفعيل)
            batch.IsOnlineLearning = false;
            await _db.SaveChangesAsync(ct);
            OnlineLearningMenuCacheSignal.Invalidate();   // OLC-S5.1: رابط القائمة يختفي فورًا

            var kept = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == batchId
                    select c.Id)
                .CountAsync(ct);

            return LectureContentResult.Ok(kept > 0
                ? $"تم إلغاء التعليم أونلاين للدفعة. لم يُحذف شيء: {kept} محتوى محفوظ ويعود للظهور عند إعادة التفعيل."
                : "تم إلغاء التعليم أونلاين للدفعة.");
        }

        // ═══════════════════════════ إنشاء المسودة (R1) ═══════════════════════════

        public async Task<LectureContentResult> CreateAsync(int lectureId, LectureContentCreateVm vm, string userId, CancellationToken ct = default)
        {
            var title = vm.Title?.Trim();
            if (string.IsNullOrEmpty(title))
                return LectureContentResult.Fail("عنوان المحتوى مطلوب.");
            if (title.Length > 200)
                return LectureContentResult.Fail("العنوان لا يتجاوز 200 حرف.");
            if (vm.ContentMode is not { } mode || !Enum.IsDefined(mode))
                return LectureContentResult.Fail("اختر وضع المحتوى.");

            var lecture = await _db.Lecture
                .AsNoTracking()
                .Where(l => l.Id == lectureId)
                .Select(l => new { l.Id, BatchIsOnline = l.Batch.IsOnlineLearning, BatchDeleted = l.Batch.IsDeleted })
                .FirstOrDefaultAsync(ct);

            if (lecture is null || lecture.BatchDeleted)
                return LectureContentResult.Missing("المحاضرة غير موجودة.");

            // R1
            if (!lecture.BatchIsOnline)
                return LectureContentResult.Fail("لا يُنشأ محتوى أونلاين إلا لمحاضرة دفعتها مفعّل لها التعليم أونلاين.");

            // الفهرس الفريد على LectureId بلا فلتر: صف محذوف ناعمًا يُعاد تفعيله بدل إنشاء صف جديد
            var existing = await _db.LectureContents
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.LectureId == lectureId, ct);

            var now = DateTime.UtcNow;

            if (existing is not null)
            {
                if (!existing.IsDeleted)
                    return LectureContentResult.Ok("المحتوى موجود بالفعل لهذه المحاضرة.", existing.Id);

                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.Status = LectureContentStatus.Draft;
                existing.ContentMode = mode;
                existing.Title = title;
                existing.Description = null;
                ClearContentVideo(existing);
                existing.ThumbnailUrl = null;
                existing.PublishedAt = null;
                existing.PublishedByUserId = null;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
                await _db.SaveChangesAsync(ct);
                return LectureContentResult.Ok("تمت استعادة المحتوى كمسودة جديدة.", existing.Id);
            }

            var entity = new LectureContent
            {
                LectureId = lectureId,
                ContentMode = mode,
                Status = LectureContentStatus.Draft,
                Title = title,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.LectureContents.Add(entity);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // سباق نقرتين: الفهرس الفريد على LectureId يرفض الثاني — نعيد معرّف الصف الذي أنشأه الطلب الأول
                _db.Entry(entity).State = EntityState.Detached;
                var winner = await _db.LectureContents.IgnoreQueryFilters().AsNoTracking()
                    .Where(c => c.LectureId == lectureId)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync(ct);
                if (winner is null)
                    throw;

                _logger.LogInformation(ex, "OLC: محتوى المحاضرة {LectureId} أُنشئ في طلب متزامن؛ أُعيد المعرّف {ContentId}.", lectureId, winner);
                return LectureContentResult.Ok("المحتوى موجود بالفعل لهذه المحاضرة.", winner);
            }

            return LectureContentResult.Ok("تم إنشاء المحتوى كمسودة. أكمل البيانات ثم احفظ.", entity.Id);
        }

        // ═══════════════════════════ البيانات الأساسية + الوضع + الفيديو الكامل ═══════════════════════════

        public async Task<LectureContentResult> UpdateAsync(int id, LectureContentUpdateVm vm, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");

            // ── الحقول النصية ──
            var title = vm.Title?.Trim();
            if (string.IsNullOrEmpty(title))
                return LectureContentResult.Fail("عنوان المحتوى مطلوب.");
            if (title.Length > 200)
                return LectureContentResult.Fail("العنوان لا يتجاوز 200 حرف.");

            var description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            if (description is { Length: > 1000 })
                return LectureContentResult.Fail("الوصف لا يتجاوز 1000 حرف.");

            if (vm.ContentMode is not { } mode || !Enum.IsDefined(mode))
                return LectureContentResult.Fail("اختر وضع المحتوى.");

            // ── الصورة المصغّرة (اختيارية): https مطلق فقط ──
            var thumbnail = string.IsNullOrWhiteSpace(vm.ThumbnailUrl) ? null : vm.ThumbnailUrl.Trim();
            if (thumbnail is not null)
            {
                if (thumbnail.Length > 500)
                    return LectureContentResult.Fail("رابط الصورة المصغّرة لا يتجاوز 500 حرف.");
                if (!Uri.TryCreate(thumbnail, UriKind.Absolute, out var thumbUri)
                    || thumbUri.Scheme != Uri.UriSchemeHttps
                    || !string.IsNullOrEmpty(thumbUri.UserInfo))
                    return LectureContentResult.Fail("رابط الصورة المصغّرة يجب أن يكون رابط https صالحًا.");
            }

            // ── تغيير الوضع ──
            var modeChanged = content.ContentMode != mode;
            List<LectureContentItem>? itemsToRemove = null;
            if (modeChanged)
            {
                if (content.Status == LectureContentStatus.Published)
                    return LectureContentResult.Fail("لا يمكن تغيير وضع محتوى منشور. أخفِ المحتوى أولًا ثم غيّر الوضع.");

                itemsToRemove = await _db.LectureContentItems.Where(i => i.LectureContentId == id).ToListAsync(ct);
                if (itemsToRemove.Count > 0 && !vm.ConfirmModeChange)
                    return LectureContentResult.Fail(
                        $"تغيير الوضع سيحذف {itemsToRemove.Count} عنصر ({(content.ContentMode == LectureContentMode.FullVideo ? "فصول" : "فيديوهات مؤشرات")}). أكّد التغيير للمتابعة.");
            }

            // ── الفيديو الكامل (R8) ──
            LectureVideoProvider? provider = null;
            string? externalId = null;
            string? canonicalUrl = null;
            int? duration = null;

            if (mode == LectureContentMode.FullVideo)
            {
                if (!string.IsNullOrWhiteSpace(vm.VideoUrl))
                {
                    if (!LectureVideoUrlParser.TryParse(vm.VideoUrl, out var p, out var extId, out var error))
                        return LectureContentResult.Fail(error ?? "رابط الفيديو غير صالح.");
                    provider = p;
                    externalId = extId;
                    canonicalUrl = LectureVideoUrlParser.BuildCanonicalUrl(p, extId);
                }
                else if (content.Status == LectureContentStatus.Published)
                {
                    // R4: رابط الفيديو إلزامي للمحتوى المنشور
                    return LectureContentResult.Fail("رابط الفيديو إلزامي للمحتوى المنشور. أخفِ المحتوى أولًا إن أردت إزالة الرابط.");
                }

                if (!string.IsNullOrWhiteSpace(vm.DurationText))
                {
                    if (!LectureTimeFormat.TryParse(vm.DurationText, out var seconds) || seconds < 1)
                        return LectureContentResult.Fail("مدة الفيديو غير صالحة. اكتبها بالصيغة mm:ss أو hh:mm:ss (حتى 12 ساعة).");
                    duration = seconds;
                }
                else if (content.Status == LectureContentStatus.Published)
                {
                    // OLC-S8.1: الإتمام يُحسب على مدة الأدمن فقط — لا تُزال من محتوى منشور
                    return LectureContentResult.Fail("مدة الفيديو إلزامية للمحتوى المنشور. أخفِ المحتوى أولًا إن أردت إزالتها.");
                }

                // R4: الفصول المحفوظة يجب أن تبقى داخل مدة الفيديو
                if (!modeChanged)
                {
                    var maxChapterEnd = await _db.LectureContentItems.AsNoTracking()
                        .Where(i => i.LectureContentId == id && i.EndSeconds != null)
                        .MaxAsync(i => i.EndSeconds, ct);
                    if (maxChapterEnd.HasValue && (duration is null || duration < maxChapterEnd))
                        return LectureContentResult.Fail(
                            $"للمحتوى فصول تنتهي عند {LectureTimeFormat.Format(maxChapterEnd)} — أدخل مدة لا تقل عنها، أو عدّل الفصول أولًا.");
                }
            }

            // ── التطبيق (SaveChanges واحد) ──
            var now = DateTime.UtcNow;

            if (itemsToRemove is { Count: > 0 })
            {
                foreach (var item in itemsToRemove)
                {
                    item.IsDeleted = true;
                    item.DeletedAt = now;
                    item.UpdatedAt = now;
                    item.UpdatedBy = userId;
                }
            }

            content.Title = title;
            content.Description = description;
            content.ContentMode = mode;
            content.ThumbnailUrl = thumbnail;

            if (mode == LectureContentMode.FullVideo)
            {
                content.VideoProvider = provider;
                content.VideoExternalId = externalId;
                content.VideoUrl = canonicalUrl;
                content.DurationSeconds = duration;
            }
            else
            {
                // الحقول على مستوى المحتوى للوضع FullVideo فقط (العقد C.2)
                ClearContentVideo(content);
            }

            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            var message = itemsToRemove is { Count: > 0 }
                ? $"تم حفظ المحتوى وتغيير الوضع، وحُذفت {itemsToRemove.Count} عناصر من الوضع السابق."
                : "تم حفظ المحتوى.";
            return LectureContentResult.Ok(message, content.Id);
        }

        // ═══════════════════════════ مساعدات ═══════════════════════════

        private static void ClearContentVideo(LectureContent content)
        {
            content.VideoProvider = null;
            content.VideoUrl = null;
            content.VideoExternalId = null;
            content.DurationSeconds = null;
        }

        private static string Truncate(string? value, int max)
        {
            var text = value?.Trim() ?? string.Empty;
            return text.Length <= max ? text : text[..max];
        }
    }
}
