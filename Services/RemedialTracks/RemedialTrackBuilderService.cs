using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S2: بناء الخطة العلاجية (الترويسة، المحاور، الفيديوهات، نموذجا 101/102، التحقق).
    /// كل كتابة داخل Transaction واحدة (إلا مزوّد InMemory في الاختبارات)، وتبديل الترتيب بمرحلتين
    /// (سالب ثم نهائي) كي لا يتعارض الفهرس الفريد (TrackId, Order) / (AxisId, Order).
    /// </summary>
    public sealed class RemedialTrackBuilderService : IRemedialTrackBuilderService
    {
        public const int MaxAxesPerTrack = 30;
        public const int MaxVideosPerAxis = 50;
        private const int MinExamMinutes = 5;
        private const int MaxExamMinutes = 180;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IRemedialTrackCodeGenerator _codes;

        public RemedialTrackBuilderService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IRemedialTrackCodeGenerator codes)
        {
            _dbFactory = dbFactory;
            _time = time;
            _codes = codes;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        // ======================================================================
        // قراءة
        // ======================================================================

        public async Task<RemedialTrackIndexVm> GetIndexAsync(RemedialTrackIndexFilter filter, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var q = db.RemedialTracks.AsNoTracking().AsQueryable();

            var search = filter.Search?.Trim();
            if (!string.IsNullOrEmpty(search))
                q = q.Where(t => t.Title.Contains(search) || t.Code.Contains(search));
            if (filter.CurriculumId is > 0)
                q = q.Where(t => t.CurriculumId == filter.CurriculumId);
            if (filter.Status is not null)
                q = q.Where(t => t.Status == filter.Status);

            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackIndexVm.PageSize);
            var page = Math.Clamp(filter.Page, 1, totalPages);

            var items = await q
                .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
                .Skip((page - 1) * RemedialTrackIndexVm.PageSize)
                .Take(RemedialTrackIndexVm.PageSize)
                .Select(t => new RemedialTrackListItemVm
                {
                    Id = t.Id,
                    Code = t.Code,
                    Title = t.Title,
                    CurriculumTitle = t.Curriculum!.Title,
                    Status = t.Status,
                    IsStructureLocked = t.IsStructureLocked,
                    AxesCount = t.Axes.Count,
                    VideosCount = t.Axes.SelectMany(a => a.Videos).Count(),
                    CreatedByName = t.CreatedByName,
                    CreatedAtUtc = t.CreatedAtUtc,
                    EnrolledCount = db.RemedialTrackEnrollments.Count(e => e.TrackId == t.Id && e.Status != RemedialTrackEnrollmentStatus.Cancelled),
                    StartedCount = db.RemedialTrackEnrollments.Count(e => e.TrackId == t.Id && e.Status != RemedialTrackEnrollmentStatus.Cancelled && e.StartedAtUtc != null)
                })
                .ToListAsync(ct);

            var tabs = await db.RemedialTracks.AsNoTracking()
                .GroupBy(t => new { t.CurriculumId, t.Curriculum!.Title })
                .Select(g => new RemedialTrackCurriculumTabVm { Id = g.Key.CurriculumId, Text = g.Key.Title, Count = g.Count() })
                .OrderBy(x => x.Text)
                .ToListAsync(ct);

            return new RemedialTrackIndexVm
            {
                CurriculumTabs = tabs,
                Items = items,
                Curricula = await LoadCurriculaAsync(db, ct),
                Search = search,
                CurriculumId = filter.CurriculumId,
                Status = filter.Status,
                Page = page,
                Total = total
            };
        }

        public async Task<List<RemedialTrackSelectOption>> GetCurriculaAsync(CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await LoadCurriculaAsync(db, ct);
        }

        private static Task<List<RemedialTrackSelectOption>> LoadCurriculaAsync(ApplicationDbContext db, CancellationToken ct)
            => db.Curriculums.AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new RemedialTrackSelectOption { Id = c.Id, Text = c.Title })
                .ToListAsync(ct);

        public async Task<RemedialTrackBuilderVm?> GetBuilderAsync(int trackId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var vm = await db.RemedialTracks.AsNoTracking()
                .Where(t => t.Id == trackId)
                .Select(t => new RemedialTrackBuilderVm
                {
                    Id = t.Id,
                    Code = t.Code,
                    Title = t.Title,
                    Description = t.Description,
                    CurriculumId = t.CurriculumId,
                    CurriculumTitle = t.Curriculum!.Title,
                    PassPercent = t.PassPercent,
                    MinWatchPercent = t.MinWatchPercent,
                    Status = t.Status,
                    IsStructureLocked = t.IsStructureLocked,
                    CreatedAtUtc = t.CreatedAtUtc,
                    UpdatedAtUtc = t.UpdatedAtUtc
                })
                .FirstOrDefaultAsync(ct);

            if (vm is null) return null;

            vm.Axes = await db.RemedialTrackAxes.AsNoTracking()
                .Where(a => a.TrackId == trackId)
                .OrderBy(a => a.Order)
                .Select(a => new RemedialTrackAxisVm
                {
                    Id = a.Id,
                    SectionId = a.SectionId,
                    SectionTitle = a.Section!.Title,
                    TitleOverride = a.TitleOverride,
                    Order = a.Order,
                    Exam101ModelId = a.Exam101ModelId,
                    Exam101Title = a.Exam101Model!.Title,
                    Exam102ModelId = a.Exam102ModelId,
                    Exam102Title = a.Exam102Model!.Title,
                    ExamDurationMinutes = a.ExamDurationMinutes,
                    ReleaseDay = a.ReleaseDay
                })
                .ToListAsync(ct);

            var videos = await db.RemedialTrackVideos.AsNoTracking()
                .Where(v => v.Axis!.TrackId == trackId)
                .OrderBy(v => v.AxisId).ThenBy(v => v.Order)
                .Select(v => new RemedialTrackVideoVm
                {
                    Id = v.Id,
                    AxisId = v.AxisId,
                    Order = v.Order,
                    Title = v.Title,
                    Url = v.Url,
                    Provider = v.Provider,
                    ExternalId = v.ExternalId,
                    DurationSeconds = v.DurationSeconds,
                    IsActive = v.IsActive
                })
                .ToListAsync(ct);

            foreach (var v in videos)
                v.EmbedUrl = RemedialTrackVideoUrlParser.BuildEmbedUrl(v.Provider, v.ExternalId);

            var byAxis = videos.ToLookup(v => v.AxisId);
            foreach (var axis in vm.Axes)
                axis.Videos = byAxis[axis.Id].ToList();

            var curriculumId = vm.CurriculumId;
            vm.AvailableSections = await db.Sections.AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId &&
                            !db.RemedialTrackAxes.Any(a => a.TrackId == trackId && a.SectionId == s.Id))
                .OrderBy(s => s.Title)
                .Select(s => new RemedialTrackSelectOption { Id = s.Id, Text = s.Title })
                .ToListAsync(ct);

            vm.ExamModels = await db.ProfessionalModels.AsNoTracking()
                .Where(m => !m.IsArchived && m.ModelType == ProfessionalModelType.Exam)
                .OrderBy(m => m.Title)
                .Select(m => new RemedialTrackSelectOption { Id = m.Id, Text = m.Title, CurriculumId = m.CurriculumId })
                .ToListAsync(ct);

            if (!vm.IsStructureLocked)
                vm.Curricula = await LoadCurriculaAsync(db, ct);

            return vm;
        }

        // ======================================================================
        // الترويسة
        // ======================================================================

        public Task<RemedialTrackResult> CreateAsync(CreateRemedialTrackInput input, RemedialTrackActor actor, CancellationToken ct = default)
            => WriteWithCodeRetryAsync(async (db, c) =>
            {
                var title = input.Title?.Trim();
                if (string.IsNullOrEmpty(title))
                    return RemedialTrackResult.Fail("⚠️ اسم الخطة مطلوب.");
                if (input.PassPercent is < 1 or > 100)
                    return RemedialTrackResult.Fail("⚠️ نسبة النجاح بين 1 و100.");
                if (input.MinWatchPercent is < 50 or > 100)
                    return RemedialTrackResult.Fail("⚠️ حد المشاهدة بين 50 و100.");
                if (!await db.Curriculums.AnyAsync(x => x.Id == input.CurriculumId, c))
                    return RemedialTrackResult.Fail("⚠️ المنهج غير موجود.");

                var track = new RemedialTrack
                {
                    Code = await _codes.NextTrackCodeAsync(c),
                    Title = title,
                    Description = NullIfBlank(input.Description),
                    CurriculumId = input.CurriculumId,
                    PassPercent = input.PassPercent,
                    MinWatchPercent = input.MinWatchPercent,
                    Status = RemedialTrackStatus.Draft,
                    CreatedByUserId = actor.UserId,
                    CreatedByName = Truncate(actor.Name, 200),
                    CreatedAtUtc = Now
                };

                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم إنشاء الخطة.", track.Id);
            }, ct);

        public Task<RemedialTrackResult> EditHeaderAsync(EditRemedialTrackHeaderInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == input.Id, c);
                var blocked = Guard(track, requireUnlocked: false);
                if (blocked is not null) return blocked;

                var title = input.Title?.Trim();
                if (string.IsNullOrEmpty(title))
                    return RemedialTrackResult.Fail("⚠️ اسم الخطة مطلوب.");

                var curriculumChanged = input.CurriculumId.HasValue && input.CurriculumId.Value != track!.CurriculumId;
                var passChanged = input.PassPercent.HasValue && input.PassPercent.Value != track!.PassPercent;
                var watchChanged = input.MinWatchPercent.HasValue && input.MinWatchPercent.Value != track!.MinWatchPercent;

                if (track!.IsStructureLocked && (curriculumChanged || passChanged || watchChanged))
                    return RemedialTrackResult.Fail("🔒 بعد أول نشر لا يمكن تغيير المنهج أو نسبة النجاح أو حد المشاهدة. انسخ الخطة لتغييرها.");

                if (input.PassPercent is < 1 or > 100)
                    return RemedialTrackResult.Fail("⚠️ نسبة النجاح بين 1 و100.");
                if (input.MinWatchPercent is < 50 or > 100)
                    return RemedialTrackResult.Fail("⚠️ حد المشاهدة بين 50 و100.");

                if (curriculumChanged)
                {
                    if (await db.RemedialTrackAxes.AnyAsync(a => a.TrackId == track.Id, c))
                        return RemedialTrackResult.Fail("⚠️ احذف محاور الخطة أولًا قبل تغيير المنهج.");
                    if (!await db.Curriculums.AnyAsync(x => x.Id == input.CurriculumId!.Value, c))
                        return RemedialTrackResult.Fail("⚠️ المنهج غير موجود.");
                    track.CurriculumId = input.CurriculumId!.Value;
                }

                track.Title = title;
                track.Description = NullIfBlank(input.Description);
                if (passChanged) track.PassPercent = input.PassPercent!.Value;
                if (watchChanged) track.MinWatchPercent = input.MinWatchPercent!.Value;
                track.UpdatedAtUtc = Now;

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم حفظ بيانات الخطة.");
            }, ct);

        // ======================================================================
        // المحاور
        // ======================================================================

        public Task<RemedialTrackResult> AddAxisAsync(AddRemedialAxisInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == input.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                if (!await db.Sections.AnyAsync(s => s.Id == input.SectionId && s.CurriculumId == track!.CurriculumId, c))
                    return RemedialTrackResult.Fail("⚠️ المحور لا يتبع منهج الخطة.");
                if (await db.RemedialTrackAxes.AnyAsync(a => a.TrackId == track!.Id && a.SectionId == input.SectionId, c))
                    return RemedialTrackResult.Fail("⚠️ هذا المحور مضاف مسبقًا للخطة.");

                var axesCount = await db.RemedialTrackAxes.CountAsync(a => a.TrackId == track!.Id, c);
                if (axesCount >= MaxAxesPerTrack)
                    return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxAxesPerTrack} محورًا في الخطة.");

                var (errors, warnings) = await ValidateExamModelsAsync(
                    db, track!.CurriculumId, input.Exam101ModelId, input.Exam102ModelId, input.ExamDurationMinutes, c);
                if (errors.Count > 0)
                    return RemedialTrackResult.Fail("⚠️ " + string.Join(" • ", errors), errors);

                var maxOrder = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == track.Id)
                    .MaxAsync(a => (int?)a.Order, c) ?? 0;

                // المحور الجديد يأخذ يوم آخر محور (قابل للتغيير من لوحة «جدول الأيام»)
                var lastDay = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == track.Id)
                    .OrderByDescending(a => a.Order)
                    .Select(a => (int?)a.ReleaseDay)
                    .FirstOrDefaultAsync(c) ?? 1;

                var axis = new RemedialTrackAxis
                {
                    TrackId = track.Id,
                    SectionId = input.SectionId,
                    Order = maxOrder + 1,
                    Exam101ModelId = input.Exam101ModelId,
                    Exam102ModelId = input.Exam102ModelId,
                    ExamDurationMinutes = input.ExamDurationMinutes,
                    ReleaseDay = lastDay
                };
                db.RemedialTrackAxes.Add(axis);
                MarkStructureChanged(track);

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تمت إضافة المحور.", axis.Id, warnings);
            }, ct);

        public Task<RemedialTrackResult> MoveAxisAsync(int axisId, int direction, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                if (direction is not (-1 or 1))
                    return RemedialTrackResult.Fail("⚠️ اتجاه غير صالح.");

                var axis = await db.RemedialTrackAxes.FirstOrDefaultAsync(a => a.Id == axisId, c);
                if (axis is null) return RemedialTrackResult.Fail("⚠️ المحور غير موجود.");

                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == axis.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var siblings = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == axis.TrackId)
                    .OrderBy(a => a.Order)
                    .ToListAsync(c);

                var index = siblings.FindIndex(a => a.Id == axisId);
                var target = index + direction;
                if (target < 0 || target >= siblings.Count)
                    return RemedialTrackResult.Fail(direction < 0 ? "ℹ️ المحور في أعلى القائمة." : "ℹ️ المحور في أسفل القائمة.");

                // الأيام تتبع الموضع لا المحور: نبدّلها مع الترتيب كي يبقى الجدول غير متناقص
                var dayA = siblings[index].ReleaseDay;
                var dayB = siblings[target].ReleaseDay;
                await SwapOrdersAsync(db, siblings[index], siblings[target], a => a.Order, (a, o) => a.Order = o, c);
                siblings[index].ReleaseDay = dayB;
                siblings[target].ReleaseDay = dayA;
                MarkStructureChanged(track!);
                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم تغيير ترتيب المحور.");
            }, ct);

        public Task<RemedialTrackResult> RemoveAxisAsync(int axisId, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var axis = await db.RemedialTrackAxes.FirstOrDefaultAsync(a => a.Id == axisId, c);
                if (axis is null) return RemedialTrackResult.Fail("⚠️ المحور غير موجود.");

                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == axis.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var removedOrder = axis.Order;
                var videos = await db.RemedialTrackVideos.Where(v => v.AxisId == axisId).ToListAsync(c);
                db.RemedialTrackVideos.RemoveRange(videos);
                db.RemedialTrackAxes.Remove(axis);
                MarkStructureChanged(track!);
                await db.SaveChangesAsync(c);

                var following = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == track!.Id && a.Order > removedOrder)
                    .OrderBy(a => a.Order)
                    .ToListAsync(c);
                await ShiftOrdersDownAsync(db, following, a => a.Order, (a, o) => a.Order = o, c);

                // لا تترك يومًا فارغًا بعد الحذف
                var remaining = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == track!.Id)
                    .OrderBy(a => a.Order)
                    .ToListAsync(c);
                var compact = RemedialTrackSchedule.Compact(remaining.Select(a => a.ReleaseDay).ToList());
                for (var i = 0; i < remaining.Count; i++) remaining[i].ReleaseDay = compact[i];
                await db.SaveChangesAsync(c);

                return RemedialTrackResult.Ok("✅ تم حذف المحور.");
            }, ct);

        public Task<RemedialTrackResult> SaveScheduleAsync(SaveRemedialScheduleInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == input.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var axes = await db.RemedialTrackAxes
                    .Where(a => a.TrackId == track!.Id)
                    .OrderBy(a => a.Order)
                    .ToListAsync(c);
                if (axes.Count == 0)
                    return RemedialTrackResult.Fail("⚠️ أضف محاور أولًا.");

                var byAxis = input.Items.GroupBy(i => i.AxisId).ToDictionary(g => g.Key, g => g.Last().Day);
                if (byAxis.Count != axes.Count || axes.Any(a => !byAxis.ContainsKey(a.Id)))
                    return RemedialTrackResult.Fail("⚠️ يجب تحديد يوم لكل محور في الخطة.");

                var days = axes.Select(a => byAxis[a.Id]).ToList();
                var errors = RemedialTrackSchedule.Validate(days);
                if (errors.Count > 0)
                    return RemedialTrackResult.Fail("⚠️ " + string.Join(" • ", errors), errors);

                for (var i = 0; i < axes.Count; i++) axes[i].ReleaseDay = days[i];
                MarkStructureChanged(track!);

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم حفظ جدول الأيام.");
            }, ct);

        public Task<RemedialTrackResult> SaveAxisExamsAsync(SaveRemedialAxisExamsInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var axis = await db.RemedialTrackAxes.FirstOrDefaultAsync(a => a.Id == input.AxisId, c);
                if (axis is null) return RemedialTrackResult.Fail("⚠️ المحور غير موجود.");

                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == axis.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var (errors, warnings) = await ValidateExamModelsAsync(
                    db, track!.CurriculumId, input.Exam101ModelId, input.Exam102ModelId, input.ExamDurationMinutes, c);
                if (errors.Count > 0)
                    return RemedialTrackResult.Fail("⚠️ " + string.Join(" • ", errors), errors);

                axis.Exam101ModelId = input.Exam101ModelId;
                axis.Exam102ModelId = input.Exam102ModelId;
                axis.ExamDurationMinutes = input.ExamDurationMinutes;
                MarkStructureChanged(track);

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم حفظ نموذجي الاختبار.", null, warnings);
            }, ct);

        // ======================================================================
        // الفيديوهات
        // ======================================================================

        public Task<RemedialTrackResult> AddVideoAsync(AddRemedialVideoInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var axis = await db.RemedialTrackAxes.FirstOrDefaultAsync(a => a.Id == input.AxisId, c);
                if (axis is null) return RemedialTrackResult.Fail("⚠️ المحور غير موجود.");

                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == axis.TrackId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var title = input.Title?.Trim();
                if (string.IsNullOrEmpty(title))
                    return RemedialTrackResult.Fail("⚠️ عنوان الفيديو مطلوب.");

                var parsed = ParseVideo(input.Url, input.DurationSeconds, out var error);
                if (error is not null) return RemedialTrackResult.Fail(error);

                var count = await db.RemedialTrackVideos.CountAsync(v => v.AxisId == axis.Id, c);
                if (count >= MaxVideosPerAxis)
                    return RemedialTrackResult.Fail($"⚠️ الحد الأقصى {MaxVideosPerAxis} فيديو في المحور.");

                var maxOrder = await db.RemedialTrackVideos
                    .Where(v => v.AxisId == axis.Id)
                    .MaxAsync(v => (int?)v.Order, c) ?? 0;

                var video = new RemedialTrackVideo
                {
                    AxisId = axis.Id,
                    Order = maxOrder + 1,
                    Title = title,
                    Url = parsed.NormalizedUrl!,
                    Provider = parsed.Provider,
                    ExternalId = parsed.ExternalId,
                    DurationSeconds = input.DurationSeconds,
                    IsActive = true
                };
                db.RemedialTrackVideos.Add(video);
                MarkStructureChanged(track!);

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تمت إضافة الفيديو.", video.Id);
            }, ct);

        public Task<RemedialTrackResult> EditVideoAsync(EditRemedialVideoInput input, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var video = await db.RemedialTrackVideos.FirstOrDefaultAsync(v => v.Id == input.VideoId, c);
                if (video is null) return RemedialTrackResult.Fail("⚠️ الفيديو غير موجود.");

                var trackId = await db.RemedialTrackAxes.Where(a => a.Id == video.AxisId).Select(a => a.TrackId).FirstAsync(c);
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == trackId, c);

                // D12: التعديل (عنوان/رابط/مدة) مسموح بعد القفل لتصحيح الأخطاء
                var blocked = Guard(track, requireUnlocked: false);
                if (blocked is not null) return blocked;

                var title = input.Title?.Trim();
                if (string.IsNullOrEmpty(title))
                    return RemedialTrackResult.Fail("⚠️ عنوان الفيديو مطلوب.");

                var parsed = ParseVideo(input.Url, input.DurationSeconds, out var error);
                if (error is not null) return RemedialTrackResult.Fail(error);

                video.Title = title;
                video.Url = parsed.NormalizedUrl!;
                video.Provider = parsed.Provider;
                video.ExternalId = parsed.ExternalId;
                video.DurationSeconds = input.DurationSeconds;

                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم تعديل الفيديو.");
            }, ct);

        public Task<RemedialTrackResult> MoveVideoAsync(int videoId, int direction, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                if (direction is not (-1 or 1))
                    return RemedialTrackResult.Fail("⚠️ اتجاه غير صالح.");

                var video = await db.RemedialTrackVideos.FirstOrDefaultAsync(v => v.Id == videoId, c);
                if (video is null) return RemedialTrackResult.Fail("⚠️ الفيديو غير موجود.");

                var track = await LoadTrackOfAxisAsync(db, video.AxisId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var siblings = await db.RemedialTrackVideos
                    .Where(v => v.AxisId == video.AxisId)
                    .OrderBy(v => v.Order)
                    .ToListAsync(c);

                var index = siblings.FindIndex(v => v.Id == videoId);
                var target = index + direction;
                if (target < 0 || target >= siblings.Count)
                    return RemedialTrackResult.Fail(direction < 0 ? "ℹ️ الفيديو في أعلى القائمة." : "ℹ️ الفيديو في أسفل القائمة.");

                await SwapOrdersAsync(db, siblings[index], siblings[target], v => v.Order, (v, o) => v.Order = o, c);
                MarkStructureChanged(track!);
                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم تغيير ترتيب الفيديو.");
            }, ct);

        public Task<RemedialTrackResult> RemoveVideoAsync(int videoId, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var video = await db.RemedialTrackVideos.FirstOrDefaultAsync(v => v.Id == videoId, c);
                if (video is null) return RemedialTrackResult.Fail("⚠️ الفيديو غير موجود.");

                var track = await LoadTrackOfAxisAsync(db, video.AxisId, c);
                var blocked = Guard(track, requireUnlocked: true);
                if (blocked is not null) return blocked;

                var axisId = video.AxisId;
                var removedOrder = video.Order;
                db.RemedialTrackVideos.Remove(video);
                MarkStructureChanged(track!);
                await db.SaveChangesAsync(c);

                var following = await db.RemedialTrackVideos
                    .Where(v => v.AxisId == axisId && v.Order > removedOrder)
                    .OrderBy(v => v.Order)
                    .ToListAsync(c);
                await ShiftOrdersDownAsync(db, following, v => v.Order, (v, o) => v.Order = o, c);

                return RemedialTrackResult.Ok("✅ تم حذف الفيديو.");
            }, ct);

        // ======================================================================
        // التحقق / الأرشفة / النسخ
        // ======================================================================

        public Task<RemedialTrackResult> MarkReadyAsync(int trackId, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == trackId, c);
                var blocked = Guard(track, requireUnlocked: false);
                if (blocked is not null) return blocked;

                var issues = await CollectReadinessIssuesAsync(db, track!, c);
                if (issues.Count > 0)
                    return RemedialTrackResult.Fail("⚠️ الخطة غير مكتملة — عالج الملاحظات التالية.", issues);

                if (track!.Status != RemedialTrackStatus.Ready)
                {
                    track.Status = RemedialTrackStatus.Ready;
                    track.UpdatedAtUtc = Now;
                    await db.SaveChangesAsync(c);
                }

                return RemedialTrackResult.Ok("✅ الخطة جاهزة للنشر.");
            }, ct);

        public Task<RemedialTrackResult> ArchiveAsync(int trackId, CancellationToken ct = default)
            => WriteAsync(async (db, c) =>
            {
                var track = await db.RemedialTracks.FirstOrDefaultAsync(t => t.Id == trackId, c);
                var blocked = Guard(track, requireUnlocked: false);
                if (blocked is not null) return blocked;

                if (await db.RemedialTrackPublications.AnyAsync(
                        p => p.TrackId == trackId && p.Status == RemedialTrackPublicationStatus.Active, c))
                    return RemedialTrackResult.Fail("⚠️ توجد أوامر نشر نشطة لهذه الخطة. ألغها أولًا ثم أرشف الخطة.");

                track!.Status = RemedialTrackStatus.Archived;
                track.UpdatedAtUtc = Now;
                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تمت أرشفة الخطة.");
            }, ct);

        public Task<RemedialTrackResult> DuplicateAsync(int trackId, RemedialTrackActor actor, CancellationToken ct = default)
            => WriteWithCodeRetryAsync(async (db, c) =>
            {
                var source = await db.RemedialTracks.AsNoTracking()
                    .Include(t => t.Axes).ThenInclude(a => a.Videos)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == trackId, c);
                if (source is null) return RemedialTrackResult.Fail("⚠️ الخطة غير موجودة.");

                var now = Now;
                var copy = new RemedialTrack
                {
                    Code = await _codes.NextTrackCodeAsync(c),
                    Title = Truncate("نسخة من " + source.Title, 200)!,
                    Description = source.Description,
                    CurriculumId = source.CurriculumId,
                    PassPercent = source.PassPercent,
                    MinWatchPercent = source.MinWatchPercent,
                    Status = RemedialTrackStatus.Draft,
                    IsStructureLocked = false,
                    CreatedByUserId = actor.UserId,
                    CreatedByName = Truncate(actor.Name, 200),
                    CreatedAtUtc = now
                };

                foreach (var a in source.Axes.OrderBy(a => a.Order))
                {
                    var axis = new RemedialTrackAxis
                    {
                        SectionId = a.SectionId,
                        TitleOverride = a.TitleOverride,
                        Order = a.Order,
                        Exam101ModelId = a.Exam101ModelId,
                        Exam102ModelId = a.Exam102ModelId,
                        ExamDurationMinutes = a.ExamDurationMinutes,
                        ReleaseDay = a.ReleaseDay
                    };
                    foreach (var v in a.Videos.OrderBy(v => v.Order))
                    {
                        axis.Videos.Add(new RemedialTrackVideo
                        {
                            Order = v.Order,
                            Title = v.Title,
                            Url = v.Url,
                            Provider = v.Provider,
                            ExternalId = v.ExternalId,
                            DurationSeconds = v.DurationSeconds,
                            IsActive = v.IsActive
                        });
                    }
                    copy.Axes.Add(axis);
                }

                db.RemedialTracks.Add(copy);
                await db.SaveChangesAsync(c);
                return RemedialTrackResult.Ok("✅ تم إنشاء نسخة من الخطة كمسودة.", copy.Id);
            }, ct);

        // ======================================================================
        // التحقق من اكتمال الخطة
        // ======================================================================

        private async Task<List<string>> CollectReadinessIssuesAsync(ApplicationDbContext db, RemedialTrack track, CancellationToken ct)
        {
            var issues = new List<string>();

            var axes = await db.RemedialTrackAxes.AsNoTracking()
                .Where(a => a.TrackId == track.Id)
                .OrderBy(a => a.Order)
                .Select(a => new
                {
                    a.Id,
                    a.TitleOverride,
                    SectionTitle = a.Section!.Title,
                    a.Exam101ModelId,
                    a.Exam102ModelId,
                    a.ExamDurationMinutes,
                    a.ReleaseDay,
                    M101 = new ModelInfo { Id = a.Exam101Model!.Id, Title = a.Exam101Model.Title, IsArchived = a.Exam101Model.IsArchived, ModelType = a.Exam101Model.ModelType, CurriculumId = a.Exam101Model.CurriculumId },
                    M102 = new ModelInfo { Id = a.Exam102Model!.Id, Title = a.Exam102Model.Title, IsArchived = a.Exam102Model.IsArchived, ModelType = a.Exam102Model.ModelType, CurriculumId = a.Exam102Model.CurriculumId }
                })
                .ToListAsync(ct);

            if (axes.Count == 0)
            {
                issues.Add("الخطة لا تحتوي أي محور.");
                return issues;
            }

            var activeVideos = await db.RemedialTrackVideos.AsNoTracking()
                .Where(v => v.Axis!.TrackId == track.Id && v.IsActive)
                .Select(v => new { v.AxisId, v.Title, v.Provider, v.DurationSeconds })
                .ToListAsync(ct);
            var videosByAxis = activeVideos.ToLookup(v => v.AxisId);

            var stats = await LoadModelStatsAsync(db,
                pmq => db.RemedialTrackAxes.Any(a => a.TrackId == track.Id &&
                                                     (a.Exam101ModelId == pmq.ModelId || a.Exam102ModelId == pmq.ModelId)),
                ct);

            foreach (var a in axes)
            {
                var name = string.IsNullOrWhiteSpace(a.TitleOverride) ? a.SectionTitle : a.TitleOverride!;
                var prefix = $"المحور «{name}»: ";

                var vids = videosByAxis[a.Id].ToList();
                if (vids.Count == 0)
                    issues.Add(prefix + "لا يحتوي فيديو فعّال.");
                foreach (var v in vids.Where(v => v.Provider == RemedialTrackVideoProvider.Other &&
                                                  (v.DurationSeconds is null || v.DurationSeconds < RemedialTrackVideoUrlParser.MinOtherDurationSeconds)))
                    issues.Add(prefix + $"الفيديو «{v.Title}» من منصة أخرى بلا مدة صالحة.");

                if (a.ExamDurationMinutes is < MinExamMinutes or > MaxExamMinutes)
                    issues.Add(prefix + $"مدة الاختبار يجب أن تكون بين {MinExamMinutes} و{MaxExamMinutes} دقيقة.");
                if (a.Exam101ModelId == a.Exam102ModelId)
                    issues.Add(prefix + "نموذجا الاختبار الأول والثاني يجب أن يكونا مختلفين.");

                CheckModel(prefix + "الاختبار الأول", a.M101, stats, issues, null, track.CurriculumId);
                CheckModel(prefix + "الاختبار الثاني", a.M102, stats, issues, null, track.CurriculumId);
            }

            foreach (var e in RemedialTrackSchedule.Validate(axes.Select(x => x.ReleaseDay).ToList()))
                issues.Add("جدول الأيام: " + e);

            return issues;
        }

        private async Task<(List<string> Errors, List<string> Warnings)> ValidateExamModelsAsync(
            ApplicationDbContext db, int trackCurriculumId, int id101, int id102, int durationMinutes, CancellationToken ct)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (durationMinutes is < MinExamMinutes or > MaxExamMinutes)
                errors.Add($"مدة الاختبار بين {MinExamMinutes} و{MaxExamMinutes} دقيقة.");
            if (id101 == id102)
                errors.Add("نموذجا الاختبار الأول والثاني يجب أن يكونا مختلفين.");
            if (errors.Count > 0) return (errors, warnings);

            var infos = await db.ProfessionalModels.AsNoTracking()
                .Where(m => m.Id == id101 || m.Id == id102)
                .Select(m => new ModelInfo { Id = m.Id, Title = m.Title, IsArchived = m.IsArchived, ModelType = m.ModelType, CurriculumId = m.CurriculumId })
                .ToListAsync(ct);

            var stats = await LoadModelStatsAsync(db, pmq => pmq.ModelId == id101 || pmq.ModelId == id102, ct);

            var m101 = infos.FirstOrDefault(i => i.Id == id101);
            var m102 = infos.FirstOrDefault(i => i.Id == id102);
            CheckModel("الاختبار الأول", m101, stats, errors, warnings, trackCurriculumId);
            CheckModel("الاختبار الثاني", m102, stats, errors, warnings, trackCurriculumId);
            return (errors, warnings);
        }

        private static void CheckModel(
            string label, ModelInfo? model, Dictionary<int, ModelStat> stats,
            List<string> errors, List<string>? warnings, int trackCurriculumId)
        {
            if (model is null)
            {
                errors.Add($"{label}: النموذج غير موجود.");
                return;
            }

            if (model.IsArchived || model.ModelType != ProfessionalModelType.Exam)
            {
                errors.Add($"{label}: النموذج «{model.Title}» يجب أن يكون نموذج اختبار غير مؤرشف.");
                return;
            }

            stats.TryGetValue(model.Id, out var stat);
            if (stat is null || stat.Total == 0)
            {
                errors.Add($"{label}: النموذج «{model.Title}» لا يحتوي أسئلة.");
            }
            else if (stat.Bad > 0)
            {
                errors.Add($"{label}: النموذج «{model.Title}» فيه {stat.Bad} سؤال بلا إجابة صحيحة (لن يعمل التصحيح).");
            }

            if (warnings is not null && model.CurriculumId.HasValue && model.CurriculumId.Value != trackCurriculumId)
                warnings.Add($"{label}: النموذج «{model.Title}» تابع لمنهج مختلف عن منهج الخطة.");
        }

        // تجميع واحد لكل النماذج المطلوبة (لا حلقة استعلامات)
        private static async Task<Dictionary<int, ModelStat>> LoadModelStatsAsync(
            ApplicationDbContext db, Expression<Func<ProfessionalModelQuestion, bool>> modelFilter, CancellationToken ct)
        {
            var rows = await (from pmq in db.ProfessionalModelQuestions.AsNoTracking().Where(modelFilter)
                              where pmq.QuestionId != null
                              join q in db.Questions.AsNoTracking() on pmq.QuestionId!.Value equals q.Id
                              group q by pmq.ModelId into g
                              select new ModelStat
                              {
                                  ModelId = g.Key,
                                  Total = g.Count(),
                                  Bad = g.Count(x => string.IsNullOrWhiteSpace(x.CorrectAnswer))
                              }).ToListAsync(ct);

            return rows.ToDictionary(r => r.ModelId);
        }

        private sealed class ModelInfo
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public bool IsArchived { get; set; }
            public ProfessionalModelType ModelType { get; set; }
            public int? CurriculumId { get; set; }
        }

        private sealed class ModelStat
        {
            public int ModelId { get; set; }
            public int Total { get; set; }
            public int Bad { get; set; }
        }

        // ======================================================================
        // أدوات مشتركة
        // ======================================================================

        private static VideoUrlParseResult ParseVideo(string? url, int? durationSeconds, out string? error)
        {
            var parsed = RemedialTrackVideoUrlParser.Parse(url);
            error = null;

            if (!parsed.IsValid)
            {
                error = "⚠️ " + parsed.Error;
            }
            else if (parsed.RequiresDuration &&
                     (durationSeconds is null || durationSeconds < RemedialTrackVideoUrlParser.MinOtherDurationSeconds))
            {
                error = $"⚠️ مدة الفيديو (بالثواني) إلزامية لهذه المنصة ولا تقل عن {RemedialTrackVideoUrlParser.MinOtherDurationSeconds}.";
            }
            else if (durationSeconds is < RemedialTrackVideoUrlParser.MinOtherDurationSeconds or > 86400)
            {
                error = "⚠️ مدة الفيديو بين 10 ثوانٍ و24 ساعة.";
            }

            return parsed;
        }

        private static RemedialTrackResult? Guard(RemedialTrack? track, bool requireUnlocked)
        {
            if (track is null)
                return RemedialTrackResult.Fail("⚠️ الخطة غير موجودة.");
            if (track.Status == RemedialTrackStatus.Archived)
                return RemedialTrackResult.Fail("🔒 الخطة مؤرشفة ولا يمكن تعديلها.");
            if (requireUnlocked && track.IsStructureLocked)
                return RemedialTrackResult.Fail("🔒 بنية الخطة مقفلة بعد أول نشر. استخدم «نسخ الخطة» لتغيير البنية.");
            return null;
        }

        // أي تعديل بنيوي على خطة Ready غير مقفلة يعيدها Draft
        private void MarkStructureChanged(RemedialTrack track)
        {
            if (track.Status == RemedialTrackStatus.Ready)
                track.Status = RemedialTrackStatus.Draft;
            track.UpdatedAtUtc = Now;
        }

        private static Task<RemedialTrack?> LoadTrackOfAxisAsync(ApplicationDbContext db, int axisId, CancellationToken ct)
            => db.RemedialTracks.FirstOrDefaultAsync(
                t => db.RemedialTrackAxes.Any(a => a.Id == axisId && a.TrackId == t.Id), ct);

        // تبديل ترتيبين على مرحلتين: سالب ثم نهائي — الفهرس الفريد لا يرى تعارضًا وسيطًا
        private static async Task SwapOrdersAsync<T>(
            ApplicationDbContext db, T a, T b, Func<T, int> get, Action<T, int> set, CancellationToken ct)
        {
            var ao = get(a);
            var bo = get(b);
            set(a, -ao);
            set(b, -bo);
            await db.SaveChangesAsync(ct);
            set(a, bo);
            set(b, ao);
            await db.SaveChangesAsync(ct);
        }

        // إعادة ترقيم العناصر اللاحقة (خفض 1) بعد حذف عنصر — مرحلتان لنفس السبب
        private static async Task ShiftOrdersDownAsync<T>(
            ApplicationDbContext db, IList<T> items, Func<T, int> get, Action<T, int> set, CancellationToken ct)
        {
            if (items.Count == 0) return;

            var olds = items.Select(get).ToList();
            foreach (var item in items) set(item, -get(item));
            await db.SaveChangesAsync(ct);
            for (var i = 0; i < items.Count; i++) set(items[i], olds[i] - 1);
            await db.SaveChangesAsync(ct);
        }

        private static string? NullIfBlank(string? s)
        {
            var t = s?.Trim();
            return string.IsNullOrEmpty(t) ? null : Truncate(t, 1000);
        }

        private static string? Truncate(string? s, int max)
            => s is null ? null : (s.Length <= max ? s : s[..max]);

        // ---------- تنفيذ الكتابة داخل Transaction + استراتيجية إعادة المحاولة ----------

        private async Task<RemedialTrackResult> WriteCoreAsync(
            Func<ApplicationDbContext, CancellationToken, Task<RemedialTrackResult>> body, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var strategy = db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear(); // أمان عند إعادة تشغيل الـ Strategy

                // مزوّد InMemory (الاختبارات) لا يدعم Transactions
                await using var tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;

                var result = await body(db, ct);
                if (result.Success && tx is not null)
                    await tx.CommitAsync(ct);
                return result; // الفشل: التخلص من tx بلا Commit = Rollback
            });
        }

        private async Task<RemedialTrackResult> WriteAsync(
            Func<ApplicationDbContext, CancellationToken, Task<RemedialTrackResult>> body, CancellationToken ct)
        {
            try
            {
                return await WriteCoreAsync(body, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ عُدّلت الخطة من مستخدم آخر. حدّث الصفحة وأعد المحاولة.");
            }
            catch (DbUpdateException)
            {
                return RemedialTrackResult.Fail("⚠️ تعذّر الحفظ بسبب تعارض في البيانات. حدّث الصفحة وأعد المحاولة.");
            }
        }

        // للإنشاء/النسخ: تعارض فهرس كود الخطة يُعاد توليده (حدّ MaxCodeRetries)
        private async Task<RemedialTrackResult> WriteWithCodeRetryAsync(
            Func<ApplicationDbContext, CancellationToken, Task<RemedialTrackResult>> body, CancellationToken ct)
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        return await WriteCoreAsync(body, ct);
                    }
                    catch (DbUpdateException) when (attempt < RemedialTrackCodeGenerator.MaxCodeRetries)
                    {
                        // تعارض محتمل على Code — أعد التوليد
                    }
                }
            }
            catch (DbUpdateException)
            {
                return RemedialTrackResult.Fail("⚠️ تعذّر إنشاء كود فريد للخطة. أعد المحاولة.");
            }
        }
    }
}
