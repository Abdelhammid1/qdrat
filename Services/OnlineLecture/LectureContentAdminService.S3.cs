using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S3 — الفصول (FullVideo) وفيديوهات المؤشرات (IndicatorVideos) ودورة النشر.
    /// القواعد: R4 (الفصول/الرابط)، R5 (عنصر صالح واحد على الأقل)، R6 (لقطة + «تحديث من المصدر» صريح)،
    /// R7 (المؤشر من محور المحاضرة)، R8 (الروابط عبر <see cref="LectureVideoUrlParser"/>).
    /// ملاحظة الفهرس الفريد (LectureContentId, LessonId) بلا فلتر: صف العنصر المحذوف ناعمًا يُعاد تفعيله لنفس المؤشر بدل إدراج صف جديد.
    /// </summary>
    public sealed partial class LectureContentAdminService
    {
        /// <summary>حالة عنصر محمَّلة باستعلام واحد (للعرض وللتحقق قبل النشر).</summary>
        private sealed record ItemState(
            int Id,
            int LessonId,
            string? LessonTitle,
            int LessonSectionId,
            string? Title,
            int SortOrder,
            int? StartSeconds,
            int? EndSeconds,
            LectureContentSourceKind SourceVideoKind,
            int? SourceVideoId,
            LectureVideoProvider? VideoProvider,
            string? VideoUrl,
            string? VideoExternalId,
            int? DurationSeconds)
        {
            public string Label => string.IsNullOrWhiteSpace(Title) ? (LessonTitle ?? $"مؤشر {LessonId}") : Title!;
        }

        // ═══════════════════════════ القراءة ═══════════════════════════

        public async Task<IReadOnlyList<LessonOptionVm>> SuggestLessonsAsync(int lectureId, CancellationToken ct = default)
        {
            var sectionId = await GetSectionIdAsync(lectureId, ct);
            return sectionId is null ? Array.Empty<LessonOptionVm>() : await LoadSectionLessonsAsync(sectionId.Value, ct);
        }

        public async Task<IReadOnlyList<IndicatorVideoOptionVm>> GetIndicatorVideoOptionsAsync(int lectureId, CancellationToken ct = default)
        {
            var sectionId = await GetSectionIdAsync(lectureId, ct);
            return sectionId is null ? Array.Empty<IndicatorVideoOptionVm>() : await LoadIndicatorVideoOptionsAsync(sectionId.Value, ct);
        }

        public async Task<int?> GetLectureIdOfItemAsync(int itemId, CancellationToken ct = default)
            => await _db.LectureContentItems
                .AsNoTracking()
                .Where(i => i.Id == itemId)
                .Select(i => (int?)i.LectureContent.LectureId)
                .FirstOrDefaultAsync(ct);

        private Task<int?> GetSectionIdAsync(int lectureId, CancellationToken ct)
            => _db.Lecture.AsNoTracking().Where(l => l.Id == lectureId).Select(l => (int?)l.SectionId).FirstOrDefaultAsync(ct);

        private async Task<List<LessonOptionVm>> LoadSectionLessonsAsync(int sectionId, CancellationToken ct)
            => await _db.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId)
                .OrderBy(l => l.UnitId).ThenBy(l => l.Id)
                .Select(l => new LessonOptionVm
                {
                    Id = l.Id,
                    Title = l.Title,
                    UnitTitle = l.Unit.Title,
                    IsActive = l.IsActive
                })
                .ToListAsync(ct);

        /// <summary>RemedialVideos النشطة لمؤشرات المحور — Join واحد (لا Contains على قائمة في الذاكرة).</summary>
        private async Task<List<IndicatorVideoOptionVm>> LoadIndicatorVideoOptionsAsync(int sectionId, CancellationToken ct)
        {
            var rows = await (
                    from r in _db.RemedialVideos.AsNoTracking()
                    join l in _db.Lessons.AsNoTracking() on r.LessonId equals l.Id
                    where r.IsActive && l.SectionId == sectionId
                    orderby r.LessonId, r.Id
                    select new { r.Id, r.LessonId, r.Title, r.VimeoUrl })
                .ToListAsync(ct);

            var options = new List<IndicatorVideoOptionVm>(rows.Count);
            foreach (var r in rows)
            {
                if (!LectureVideoUrlParser.TryParse(r.VimeoUrl, out var provider, out _))
                {
                    _logger.LogWarning("OLC: فيديو المؤشر {RemedialVideoId} مستبعد من الاختيار لأن رابطه غير صالح وفق R8.", r.Id);
                    continue;
                }
                options.Add(new IndicatorVideoOptionVm { Id = r.Id, LessonId = r.LessonId, Title = r.Title ?? $"فيديو {r.Id}", Provider = provider });
            }
            return options;
        }

        private Task<List<ItemState>> LoadItemsAsync(int contentId, CancellationToken ct)
            => _db.LectureContentItems
                .AsNoTracking()
                .Where(i => i.LectureContentId == contentId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new ItemState(
                    i.Id, i.LessonId, i.Lesson.Title, i.Lesson.SectionId, i.Title, i.SortOrder,
                    i.StartSeconds, i.EndSeconds, i.SourceVideoKind, i.SourceVideoId,
                    i.VideoProvider, i.VideoUrl, i.VideoExternalId, i.DurationSeconds))
                .ToListAsync(ct);

        /// <summary>يملأ أقسام S3 في صفحة الأدمن. يُستدعى من <see cref="GetForLectureAsync"/> عند وجود محتوى.</summary>
        private async Task FillEditorAsync(LectureContentEditVm vm, int? curriculumId, string? contentExternalId, CancellationToken ct)
        {
            var contentId = vm.ContentId!.Value;
            var lessons = await LoadSectionLessonsAsync(vm.SectionId, ct);
            var items = await LoadItemsAsync(contentId, ct);
            vm.LessonOptions = lessons;

            if (vm.ContentMode == LectureContentMode.FullVideo)
            {
                vm.Chapters = BuildChapterRows(lessons, items, vm.SectionId);
            }
            else
            {
                var sources = await LoadRemedialSourcesAsync(contentId, ct);
                vm.Items = items.Select(i => ToRow(i, vm.SectionId, sources)).ToList();
                vm.IndicatorVideoOptions = await LoadIndicatorVideoOptionsAsync(vm.SectionId, ct);
            }

            vm.PublishIssues = CollectPublishIssues(vm.ContentMode!.Value, vm.VideoProvider, contentExternalId,
                vm.DurationSeconds, items, vm.SectionId, vm.BatchIsOnline);

            // ── تنبيهات لا تمنع النشر ──
            if (vm.ContentMode == LectureContentMode.FullVideo)
            {
                if (vm.DurationSeconds is null)
                    vm.PublishWarnings.Add("لم تُدخل مدة الفيديو — سيُعتمد على المدة التي يبلّغ بها المشغّل لاحتساب نسبة المشاهدة (80%).");
                if (items.Count == 0)
                    vm.PublishWarnings.Add("لا توجد فصول — سيرى الطالب الفيديو كاملًا بلا تقسيم للمؤشرات.");
            }
            else
            {
                var withoutDuration = items.Count(i => i.DurationSeconds is null);
                if (withoutDuration > 0)
                    vm.PublishWarnings.Add($"{withoutDuration} فيديو بلا مدة مُدخلة — سيُعتمد على المدة التي يبلّغ بها المشغّل.");
            }

            if (vm.Status != LectureContentStatus.Published && curriculumId.HasValue)
            {
                var later = await CountLaterPublishedInCurriculumAsync(vm, curriculumId.Value, ct);
                if (later > 0)
                    vm.PublishWarnings.Add(
                        $"توجد {later} محاضرة منشورة بعد هذه المحاضرة في نفس المنهج. عند النشر تُقفل المحاضرات اللاحقة غير المكتملة عند كل طالب حتى يُتمّ هذه المحاضرة أولًا، ولا يتأثر ما اكتمل (R15).");
            }
        }

        /// <summary>صفوف محرر الفصول: كل مؤشر نشط في المحور + أي فصل محفوظ (ولو لمؤشر غير نشط أو خارج المحور).</summary>
        private static List<LectureContentChapterInputVm> BuildChapterRows(List<LessonOptionVm> lessons, List<ItemState> items, int sectionId)
        {
            var byLesson = items.ToDictionary(i => i.LessonId);
            var rows = new List<LectureContentChapterInputVm>();

            foreach (var lesson in lessons)
            {
                byLesson.TryGetValue(lesson.Id, out var item);
                if (item is null && !lesson.IsActive)
                    continue;

                rows.Add(new LectureContentChapterInputVm
                {
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    UnitTitle = lesson.UnitTitle,
                    Include = item is not null,
                    Title = item?.Title,
                    StartText = LectureTimeFormat.Format(item?.StartSeconds),
                    EndText = LectureTimeFormat.Format(item?.EndSeconds)
                });
            }

            // فصول محفوظة لمؤشر خارج محور المحاضرة (R7) — تُعرض ليزيلها الأدمن
            foreach (var item in items.Where(i => i.LessonSectionId != sectionId))
            {
                rows.Add(new LectureContentChapterInputVm
                {
                    LessonId = item.LessonId,
                    LessonTitle = item.LessonTitle ?? $"مؤشر {item.LessonId}",
                    Include = true,
                    OutsideSection = true,
                    Title = item.Title,
                    StartText = LectureTimeFormat.Format(item.StartSeconds),
                    EndText = LectureTimeFormat.Format(item.EndSeconds)
                });
            }

            return rows;
        }

        /// <summary>المصدر الحالي لعناصر RemedialVideo (Join واحد) — لكشف تغيّر المصدر بعد اللقطة (R6).</summary>
        private async Task<Dictionary<int, (string? Url, bool IsActive, int LessonId)>> LoadRemedialSourcesAsync(int contentId, CancellationToken ct)
        {
            var rows = await (
                    from i in _db.LectureContentItems.AsNoTracking()
                    join r in _db.RemedialVideos.AsNoTracking() on i.SourceVideoId equals (int?)r.Id
                    where i.LectureContentId == contentId && i.SourceVideoKind == LectureContentSourceKind.RemedialVideo
                    select new { ItemId = i.Id, r.VimeoUrl, r.IsActive, r.LessonId })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.ItemId, r => ((string?)r.VimeoUrl, r.IsActive, r.LessonId));
        }

        private static LectureContentItemRowVm ToRow(ItemState i, int sectionId, Dictionary<int, (string? Url, bool IsActive, int LessonId)> sources)
        {
            var row = new LectureContentItemRowVm
            {
                Id = i.Id,
                LessonId = i.LessonId,
                LessonTitle = i.LessonTitle ?? $"مؤشر {i.LessonId}",
                Title = i.Title,
                SortOrder = i.SortOrder,
                SourceVideoKind = i.SourceVideoKind,
                SourceVideoId = i.SourceVideoId,
                VideoProvider = i.VideoProvider,
                VideoUrl = i.VideoUrl,
                DurationSeconds = i.DurationSeconds,
                EmbedUrl = LectureVideoUrlParser.TryBuildEmbedUrl(i.VideoProvider, i.VideoExternalId),
                OutsideSection = i.LessonSectionId != sectionId
            };

            if (i.SourceVideoKind == LectureContentSourceKind.RemedialVideo)
            {
                if (!sources.TryGetValue(i.Id, out var src)
                    || !src.IsActive
                    || src.LessonId != i.LessonId
                    || !LectureVideoUrlParser.TryParse(src.Url, out var p, out var extId))
                {
                    row.SourceUnavailable = true;
                }
                else
                {
                    row.SourceChanged = p != i.VideoProvider || !string.Equals(extId, i.VideoExternalId, StringComparison.Ordinal);
                }
            }

            return row;
        }

        private async Task<int> CountLaterPublishedInCurriculumAsync(LectureContentEditVm vm, int curriculumId, CancellationToken ct)
        {
            var lectureId = vm.LectureId;
            var others = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == vm.BatchId
                       && l.Id != lectureId
                       && c.Status == LectureContentStatus.Published
                       && l.Section.CurriculumId == curriculumId
                    select new { l.Id, l.Date, l.ScheduledTime })
                .ToListAsync(ct);

            // ترتيب R13: (Date, ScheduledTime ?? 00:00, Id)
            var mine = (vm.LectureDate, vm.ScheduledTime ?? TimeSpan.Zero, lectureId);
            return others.Count(o => Compare((o.Date, o.ScheduledTime ?? TimeSpan.Zero, o.Id), mine) > 0);

            static int Compare((DateTime D, TimeSpan T, int Id) a, (DateTime D, TimeSpan T, int Id) b)
            {
                var c = a.D.CompareTo(b.D);
                if (c != 0) return c;
                c = a.T.CompareTo(b.T);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            }
        }

        /// <summary>موانع النشر (R1/R4/R5/R7/R8) على بيانات محمَّلة — نفس الدالة للعرض وللنشر الفعلي.</summary>
        private static List<string> CollectPublishIssues(
            LectureContentMode mode,
            LectureVideoProvider? provider,
            string? externalId,
            int? durationSeconds,
            IReadOnlyList<ItemState> items,
            int sectionId,
            bool batchIsOnline)
        {
            var issues = new List<string>();

            if (!batchIsOnline)
                issues.Add("الدفعة غير مفعّل لها التعليم أونلاين — فعّله من شاشة تعديل الدفعة أولًا (R1).");

            foreach (var outside in items.Where(i => i.LessonSectionId != sectionId))
                issues.Add($"المؤشر «{outside.LessonTitle}» لا ينتمي لمحور المحاضرة — أزله (R7).");

            if (mode == LectureContentMode.FullVideo)
            {
                if (provider is not { } p || !LectureVideoUrlParser.IsValidId(p, externalId))
                    issues.Add("أضف رابط الفيديو الكامل (YouTube/Vimeo) من تبويب «البيانات والوضع» (R4).");

                issues.AddRange(LectureChapterRules.Validate(
                    items.Select(i => new ChapterTiming(i.Label, i.StartSeconds, i.EndSeconds)).ToList(),
                    durationSeconds));
            }
            else
            {
                if (items.Count == 0)
                    issues.Add("أضف فيديو مؤشر واحدًا على الأقل إلى القائمة (R5).");

                foreach (var bad in items.Where(i => i.VideoProvider is not { } ip || !LectureVideoUrlParser.IsValidId(ip, i.VideoExternalId)))
                    issues.Add($"فيديو المؤشر «{bad.Label}» بلا رابط صالح — احذفه وأعد إضافته (R5).");
            }

            return issues;
        }

        // ═══════════════════════════ OLC-S3.1 — الفصول ═══════════════════════════

        public async Task<LectureContentResult> SetChaptersAsync(int id, IReadOnlyList<LectureContentChapterInputVm> chapters, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");
            if (content.ContentMode != LectureContentMode.FullVideo)
                return LectureContentResult.Fail("الفصول للوضع «فيديو كامل» فقط.");

            var sectionId = await GetSectionIdAsync(content.LectureId, ct);
            if (sectionId is null)
                return LectureContentResult.Missing("المحاضرة غير موجودة.");

            var selected = (chapters ?? Array.Empty<LectureContentChapterInputVm>()).Where(c => c.Include).ToList();
            if (selected.GroupBy(c => c.LessonId).Any(g => g.Count() > 1))
                return LectureContentResult.Fail("لا يتكرر المؤشر في أكثر من فصل.");

            // R7: كل مؤشر من محور المحاضرة (قائمة المحور تُحمَّل مرة واحدة — لا Contains داخل EF)
            var sectionLessons = (await LoadSectionLessonsAsync(sectionId.Value, ct)).ToDictionary(l => l.Id, l => l.Title);

            var parsed = new List<(int LessonId, string? Title, int? Start, int? End, string Label)>(selected.Count);
            var errors = new List<string>();
            foreach (var c in selected)
            {
                if (!sectionLessons.TryGetValue(c.LessonId, out var lessonTitle))
                {
                    errors.Add($"المؤشر رقم {c.LessonId} لا ينتمي لمحور المحاضرة — أزله من الفصول (R7).");
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(c.Title) ? null : c.Title.Trim();
                if (title is { Length: > 200 })
                {
                    errors.Add($"عنوان فصل «{lessonTitle}» لا يتجاوز 200 حرف.");
                    continue;
                }

                int? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(c.StartText))
                {
                    if (LectureTimeFormat.TryParse(c.StartText, out var s)) start = s;
                    else errors.Add($"بداية الفصل «{lessonTitle}» غير صالحة. اكتبها mm:ss أو hh:mm:ss.");
                }
                if (!string.IsNullOrWhiteSpace(c.EndText))
                {
                    if (LectureTimeFormat.TryParse(c.EndText, out var e)) end = e;
                    else errors.Add($"نهاية الفصل «{lessonTitle}» غير صالحة. اكتبها mm:ss أو hh:mm:ss.");
                }

                parsed.Add((c.LessonId, title, start, end, title ?? lessonTitle));
            }

            if (errors.Count == 0)
                errors.AddRange(LectureChapterRules.Validate(parsed.Select(p => new ChapterTiming(p.Label, p.Start, p.End)).ToList(), content.DurationSeconds));
            if (errors.Count > 0)
                return LectureContentResult.Fail(string.Join(" · ", errors));

            // ── التطبيق: SaveChanges واحد. SortOrder = ترتيب البداية (R4). ──
            var now = DateTime.UtcNow;
            var existing = await _db.LectureContentItems.IgnoreQueryFilters()
                .Where(i => i.LectureContentId == id)
                .ToListAsync(ct);
            var existingByLesson = existing.ToDictionary(i => i.LessonId);

            var ordered = parsed.OrderBy(p => p.Start).ThenBy(p => p.End).ToList();
            var keep = new HashSet<int>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var p = ordered[index];
                keep.Add(p.LessonId);

                if (!existingByLesson.TryGetValue(p.LessonId, out var item))
                {
                    item = new LectureContentItem { LectureContentId = id, LessonId = p.LessonId, CreatedAt = now, CreatedBy = userId };
                    _db.LectureContentItems.Add(item);
                }
                else
                {
                    item.UpdatedAt = now;
                    item.UpdatedBy = userId;
                }

                item.IsDeleted = false;
                item.DeletedAt = null;
                item.SortOrder = index + 1;
                item.Title = p.Title;
                item.StartSeconds = p.Start;
                item.EndSeconds = p.End;
                // الفصل جزء من الفيديو الكامل: لا مصدر ولا رابط مستقل
                item.SourceVideoKind = LectureContentSourceKind.Manual;
                item.SourceVideoId = null;
                item.VideoProvider = null;
                item.VideoUrl = null;
                item.VideoExternalId = null;
                item.DurationSeconds = null;
            }

            var removed = 0;
            foreach (var item in existing.Where(i => !i.IsDeleted && !keep.Contains(i.LessonId)))
            {
                item.IsDeleted = true;
                item.DeletedAt = now;
                item.UpdatedAt = now;
                item.UpdatedBy = userId;
                removed++;
            }

            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            var message = ordered.Count == 0
                ? (removed > 0 ? "أُزيلت كل الفصول — سيظهر الفيديو كاملًا بلا تقسيم." : "لا توجد فصول محددة.")
                : $"تم حفظ {ordered.Count} فصل مرتبة بتوقيت البداية.";
            return LectureContentResult.Ok(message, id);
        }

        // ═══════════════════════════ OLC-S3.2 — فيديوهات المؤشرات ═══════════════════════════

        public async Task<LectureContentResult> AddIndicatorVideoAsync(int id, AddIndicatorVideoVm vm, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");
            if (content.ContentMode != LectureContentMode.IndicatorVideos)
                return LectureContentResult.Fail("إضافة فيديوهات المؤشرات للوضع «فيديوهات المؤشرات» فقط.");
            if (vm.LessonId is not { } lessonId)
                return LectureContentResult.Fail("اختر المؤشر.");

            var sectionId = await GetSectionIdAsync(content.LectureId, ct);
            if (sectionId is null)
                return LectureContentResult.Missing("المحاضرة غير موجودة.");

            // R7
            var lessonTitle = await _db.Lessons.AsNoTracking()
                .Where(l => l.Id == lessonId && l.SectionId == sectionId.Value)
                .Select(l => l.Title)
                .FirstOrDefaultAsync(ct);
            if (lessonTitle is null)
                return LectureContentResult.Fail("المؤشر المختار لا ينتمي لمحور المحاضرة (R7).");

            var title = string.IsNullOrWhiteSpace(vm.Title) ? null : vm.Title.Trim();
            if (title is { Length: > 200 })
                return LectureContentResult.Fail("العنوان لا يتجاوز 200 حرف.");

            int? duration = null;
            if (!string.IsNullOrWhiteSpace(vm.DurationText))
            {
                if (!LectureTimeFormat.TryParse(vm.DurationText, out var seconds) || seconds < 1)
                    return LectureContentResult.Fail("مدة الفيديو غير صالحة. اكتبها بالصيغة mm:ss أو hh:mm:ss.");
                duration = seconds;
            }

            // ── المصدر + اللقطة (R6) ──
            LectureVideoProvider provider;
            string externalId;
            int? sourceId = null;
            switch (vm.SourceKind)
            {
                case LectureContentSourceKind.RemedialVideo:
                {
                    if (vm.SourceVideoId is not { } sid)
                        return LectureContentResult.Fail("اختر فيديو المؤشر من القائمة.");
                    var source = await _db.RemedialVideos.AsNoTracking()
                        .Where(r => r.Id == sid && r.LessonId == lessonId && r.IsActive)
                        .Select(r => new { r.Id, r.VimeoUrl })
                        .FirstOrDefaultAsync(ct);
                    if (source is null)
                        return LectureContentResult.Fail("فيديو المؤشر المختار غير متاح أو لا يخص هذا المؤشر.");
                    if (!LectureVideoUrlParser.TryParse(source.VimeoUrl, out provider, out externalId, out var srcError))
                        return LectureContentResult.Fail("رابط فيديو المؤشر المحفوظ غير مقبول: " + srcError);
                    sourceId = source.Id;
                    break;
                }
                case LectureContentSourceKind.Manual:
                {
                    if (!LectureVideoUrlParser.TryParse(vm.VideoUrl, out provider, out externalId, out var error))
                        return LectureContentResult.Fail(error ?? "رابط الفيديو غير صالح.");
                    break;
                }
                default:
                    return LectureContentResult.Fail("مصدر الفيديو غير مدعوم.");
            }

            var existing = await _db.LectureContentItems.IgnoreQueryFilters()
                .Where(i => i.LectureContentId == id)
                .ToListAsync(ct);
            if (existing.Any(i => !i.IsDeleted && i.LessonId == lessonId))
                return LectureContentResult.Fail($"المؤشر «{lessonTitle}» مضاف بالفعل في القائمة. احذفه أولًا لاستبدال فيديوه.");

            var now = DateTime.UtcNow;
            var nextOrder = existing.Where(i => !i.IsDeleted).Select(i => i.SortOrder).DefaultIfEmpty(0).Max() + 1;

            var item = existing.FirstOrDefault(i => i.LessonId == lessonId);
            if (item is null)
            {
                item = new LectureContentItem { LectureContentId = id, LessonId = lessonId, CreatedAt = now, CreatedBy = userId };
                _db.LectureContentItems.Add(item);
            }
            else
            {
                item.UpdatedAt = now;
                item.UpdatedBy = userId;
            }

            item.IsDeleted = false;
            item.DeletedAt = null;
            item.SortOrder = nextOrder;
            item.Title = title;
            item.StartSeconds = null;
            item.EndSeconds = null;
            item.SourceVideoKind = vm.SourceKind;
            item.SourceVideoId = sourceId;
            item.VideoProvider = provider;
            item.VideoExternalId = externalId;
            item.VideoUrl = LectureVideoUrlParser.BuildCanonicalUrl(provider, externalId);
            item.DurationSeconds = duration;

            content.UpdatedAt = now;
            content.UpdatedBy = userId;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // سباق نقرتين على نفس المؤشر: الفهرس الفريد (LectureContentId, LessonId) يرفض الثاني
                _logger.LogInformation(ex, "OLC: إضافة مكررة للمؤشر {LessonId} في المحتوى {ContentId}.", lessonId, id);
                return LectureContentResult.Fail($"المؤشر «{lessonTitle}» مضاف بالفعل في القائمة.");
            }

            return LectureContentResult.Ok($"أُضيف فيديو المؤشر «{lessonTitle}» إلى نهاية القائمة.", id);
        }

        public async Task<LectureContentResult> RemoveItemAsync(int itemId, string userId, CancellationToken ct = default)
        {
            var item = await _db.LectureContentItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is null)
                return LectureContentResult.Missing("العنصر غير موجود.");

            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == item.LectureContentId, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");

            if (content.Status == LectureContentStatus.Published && content.ContentMode == LectureContentMode.IndicatorVideos)
            {
                var activeCount = await _db.LectureContentItems.CountAsync(i => i.LectureContentId == content.Id, ct);
                if (activeCount <= 1)
                    return LectureContentResult.Fail("لا يمكن حذف آخر فيديو في محتوى منشور (R5). أضف بديلًا أولًا أو أخفِ المحتوى.");
            }

            var now = DateTime.UtcNow;
            item.IsDeleted = true;
            item.DeletedAt = now;
            item.UpdatedAt = now;
            item.UpdatedBy = userId;
            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            return LectureContentResult.Ok(content.ContentMode == LectureContentMode.FullVideo ? "أُزيل الفصل." : "حُذف الفيديو من القائمة.", content.Id);
        }

        public async Task<LectureContentResult> ReorderItemsAsync(int id, IReadOnlyList<int> orderedItemIds, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");
            if (content.ContentMode != LectureContentMode.IndicatorVideos)
                return LectureContentResult.Fail("ترتيب الفصول يتبع توقيت البداية تلقائيًا — لا يُعاد ترتيبها يدويًا.");

            var items = await _db.LectureContentItems.Where(i => i.LectureContentId == id).ToListAsync(ct);
            var ids = orderedItemIds ?? Array.Empty<int>();
            if (ids.Count != items.Count || ids.Distinct().Count() != ids.Count || !items.All(i => ids.Contains(i.Id)))
                return LectureContentResult.Fail("الترتيب المرسل لا يطابق عناصر القائمة الحالية. حدّث الصفحة وأعد المحاولة.");

            var position = ids.Select((itemId, index) => (itemId, index)).ToDictionary(x => x.itemId, x => x.index + 1);
            var now = DateTime.UtcNow;
            var changed = 0;
            foreach (var item in items)
            {
                var order = position[item.Id];
                if (item.SortOrder == order)
                    continue;
                item.SortOrder = order;
                item.UpdatedAt = now;
                item.UpdatedBy = userId;
                changed++;
            }

            if (changed == 0)
                return LectureContentResult.Ok("الترتيب لم يتغيّر.", id);

            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return LectureContentResult.Ok("تم حفظ ترتيب القائمة.", id);
        }

        public async Task<LectureContentResult> RefreshItemFromSourceAsync(int itemId, string userId, CancellationToken ct = default)
        {
            var item = await _db.LectureContentItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is null)
                return LectureContentResult.Missing("العنصر غير موجود.");
            if (item.SourceVideoKind != LectureContentSourceKind.RemedialVideo || item.SourceVideoId is not { } sourceId)
                return LectureContentResult.Fail("هذا الفيديو أُضيف برابط يدوي — لا مصدر لتحديثه منه.", item.LectureContentId);

            var source = await _db.RemedialVideos.AsNoTracking()
                .Where(r => r.Id == sourceId)
                .Select(r => new { r.VimeoUrl, r.IsActive, r.LessonId })
                .FirstOrDefaultAsync(ct);

            if (source is null || !source.IsActive)
                return LectureContentResult.Fail("فيديو المصدر لم يعد متاحًا — اللقطة الحالية تبقى تعمل كما هي.", item.LectureContentId);
            if (source.LessonId != item.LessonId)
                return LectureContentResult.Fail("فيديو المصدر نُقل إلى مؤشر آخر — اللقطة الحالية تبقى كما هي.", item.LectureContentId);
            if (!LectureVideoUrlParser.TryParse(source.VimeoUrl, out var provider, out var externalId, out var error))
                return LectureContentResult.Fail("رابط المصدر الحالي غير مقبول: " + error, item.LectureContentId);

            if (item.VideoProvider == provider && string.Equals(item.VideoExternalId, externalId, StringComparison.Ordinal))
                return LectureContentResult.Ok("اللقطة مطابقة للمصدر بالفعل.", item.LectureContentId);

            var now = DateTime.UtcNow;
            item.VideoProvider = provider;
            item.VideoExternalId = externalId;
            item.VideoUrl = LectureVideoUrlParser.BuildCanonicalUrl(provider, externalId);
            item.UpdatedAt = now;
            item.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            return LectureContentResult.Ok("تم تحديث الفيديو من المصدر.", item.LectureContentId);
        }

        // ═══════════════════════════ OLC-S3.3 — النشر ═══════════════════════════

        public async Task<LectureContentResult> PublishAsync(int id, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");

            if (content.Status == LectureContentStatus.Published)
                return LectureContentResult.Ok("المحتوى منشور بالفعل.", id);

            var lecture = await _db.Lecture.AsNoTracking()
                .Where(l => l.Id == content.LectureId)
                .Select(l => new { l.SectionId, BatchIsOnline = l.Batch.IsOnlineLearning && !l.Batch.IsDeleted })
                .FirstOrDefaultAsync(ct);
            if (lecture is null)
                return LectureContentResult.Missing("المحاضرة غير موجودة.");

            var items = await LoadItemsAsync(id, ct);
            var issues = CollectPublishIssues(content.ContentMode, content.VideoProvider, content.VideoExternalId,
                content.DurationSeconds, items, lecture.SectionId, lecture.BatchIsOnline);
            if (issues.Count > 0)
                return LectureContentResult.Fail("لا يمكن النشر: " + string.Join(" · ", issues));

            var now = DateTime.UtcNow;
            content.Status = LectureContentStatus.Published;
            // PublishedAt/By = أول نشر (أساس KPI «زمن النشر بعد المحاضرة»)؛ إعادة النشر بعد الإخفاء لا تغيّرهما
            if (content.PublishedAt is null)
            {
                content.PublishedAt = now;
                content.PublishedByUserId = userId;
            }
            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            return LectureContentResult.Ok("تم نشر المحتوى لطلاب الدفعة.", id);
        }

        public async Task<LectureContentResult> HideAsync(int id, string userId, CancellationToken ct = default)
        {
            var content = await _db.LectureContents.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (content is null)
                return LectureContentResult.Missing("المحتوى غير موجود.");

            switch (content.Status)
            {
                case LectureContentStatus.Hidden:
                    return LectureContentResult.Ok("المحتوى مخفي بالفعل.", id);
                case LectureContentStatus.Draft:
                    return LectureContentResult.Fail("المسودة غير ظاهرة للطلاب أصلًا — لا حاجة لإخفائها.");
            }

            var now = DateTime.UtcNow;
            content.Status = LectureContentStatus.Hidden;
            content.UpdatedAt = now;
            content.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);

            return LectureContentResult.Ok("أُخفي المحتوى عن الطلاب. تقدّمهم محفوظ ويعود عند إعادة النشر.", id);
        }
    }
}
