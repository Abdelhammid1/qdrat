using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S6.1: سجل الإرسال للمدارس الشريكة — قراءة فقط.
    // الترقيم على مستوى «ضغطة الإرسال» (DispatchGroupId)، والأسطر تُحمَّل لصفحة واحدة فقط (لا N+1).
    // عند فلترة المدرسة أو التجاوز يعرض كل إرسال الأسطر المطابقة فقط، وأعداده محسوبة منها.
    public class PartnerDispatchLogReportService : IPartnerDispatchLogReportService
    {
        private static readonly Lazy<TimeZoneInfo> DisplayZone = new(() =>
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"); }
        });

        private readonly ApplicationDbContext _context;

        public PartnerDispatchLogReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public static DateTime ToDisplayTime(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), DisplayZone.Value);

        public async Task<PartnerDispatchLogPage> SearchAsync(PartnerDispatchLogFilter filter, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            var pageSize = IPartnerDispatchLogReportService.PageSize;
            var logs = Filtered(filter);

            var totals = await logs
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Lines = g.Count(),
                    Students = g.Sum(l => l.StudentsCount),
                    Overridden = g.Sum(l => l.ContractOverridden ? 1 : 0)
                })
                .FirstOrDefaultAsync(ct);

            var totalGroups = totals == null ? 0 : await logs.Select(l => l.DispatchGroupId).Distinct().CountAsync(ct);
            var totalPages = (int)Math.Ceiling(totalGroups / (double)pageSize);
            var page = Math.Clamp(filter.Page, 1, Math.Max(totalPages, 1));

            if (totalGroups == 0)
                return new PartnerDispatchLogPage { Page = 1, PageSize = pageSize };

            var groupKeys = await logs
                .GroupBy(l => l.DispatchGroupId)
                .Select(g => new { g.Key, SentAtUtc = g.Min(l => l.SentAtUtc) })
                .OrderByDescending(g => g.SentAtUtc)
                .ThenBy(g => g.Key)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // ≤ 20 قيمة؛ مع UseCompatibilityLevel(120) تُترجم إلى IN بثوابت لا OPENJSON
            var groupIds = groupKeys.Select(g => g.Key).ToList();
            var lines = await ProjectLines(logs.Where(l => groupIds.Contains(l.DispatchGroupId)))
                .ToListAsync(ct);

            var byGroup = lines.ToLookup(l => l.DispatchGroupId);
            var groups = groupKeys
                .Select(k =>
                {
                    var groupLines = byGroup[k.Key]
                        .OrderBy(l => l.PartnerName)
                        .ThenBy(l => l.BatchName)
                        .ThenBy(l => l.Id)
                        .ToList();
                    var first = groupLines.First();
                    return new PartnerDispatchLogGroup
                    {
                        DispatchGroupId = k.Key,
                        Kind = first.Kind,
                        Title = first.Title,
                        SentByName = first.SentByName,
                        SentAtUtc = k.SentAtUtc,
                        PartnersCount = groupLines.Select(l => l.PartnerId).Distinct().Count(),
                        BatchesCount = groupLines.Count,
                        StudentsCount = groupLines.Sum(l => l.StudentsCount),
                        AnyOverridden = groupLines.Any(l => l.ContractOverridden),
                        HasDraft = groupLines.Any(l => l.DraftState != PartnerDispatchDraftState.None),
                        Lines = groupLines
                    };
                })
                .ToList();

            return new PartnerDispatchLogPage
            {
                Groups = groups,
                Page = page,
                PageSize = pageSize,
                TotalGroups = totalGroups,
                TotalLines = totals!.Lines,
                TotalStudents = totals.Students,
                OverriddenLines = totals.Overridden
            };
        }

        public async Task<PartnerDispatchLogFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)
        {
            var partners = await _context.Partners.AsNoTracking()
                .Where(p => _context.PartnerDispatchLogs.Any(l => l.PartnerId == p.Id))
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(ct);

            var senders = await _context.Users.AsNoTracking()
                .Where(u => _context.PartnerDispatchLogs.Any(l => l.SentByUserId == u.Id))
                .Select(u => new { u.Id, u.FullName, u.UserName })
                .ToListAsync(ct);

            return new PartnerDispatchLogFilterOptions
            {
                Partners = partners
                    .Select(p => new PartnerDispatchLogOption(p.Id.ToString(), p.Name))
                    .ToList(),
                Senders = senders
                    .Select(u => new PartnerDispatchLogOption(u.Id, DisplayName(u.FullName, u.UserName, u.Id)))
                    .OrderBy(o => o.Text)
                    .ToList()
            };
        }

        public async Task<PartnerDispatchLogExport> ExportAsync(PartnerDispatchLogFilter filter, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            var max = IPartnerDispatchLogReportService.MaxExportRows;

            var lines = await ProjectLines(Filtered(filter))
                .OrderByDescending(l => l.SentAtUtc)
                .ThenBy(l => l.DispatchGroupId)
                .ThenBy(l => l.Id)
                .Take(max + 1)
                .ToListAsync(ct);

            var truncated = lines.Count > max;
            if (truncated)
                lines.RemoveAt(lines.Count - 1);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("سجل الإرسال");
            sheet.RightToLeft = true;

            var headers = new[]
            {
                "رقم السطر", "معرّف الإرسال", "النوع", "العنوان", "المدرسة", "الدفعة", "الفرع",
                "رقم العنصر المُنشأ", "عدد الطلاب", "حالة المسودة", "تجاوز العقد", "سبب التجاوز", "المرسل", "التاريخ (توقيت السعودية)"
            };
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];

            var header = sheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");

            var row = 2;
            foreach (var line in lines)
            {
                // القيم النصية تُكتب كنص (ClosedXML ≥ 0.100 لا يحوّل «=» إلى صيغة)
                sheet.Cell(row, 1).Value = line.Id;
                sheet.Cell(row, 2).Value = line.DispatchGroupId.ToString();
                sheet.Cell(row, 3).Value = PartnerDispatchKindLabels.Of(line.Kind);
                sheet.Cell(row, 4).Value = line.Title;
                sheet.Cell(row, 5).Value = line.PartnerName;
                sheet.Cell(row, 6).Value = line.BatchName ?? $"#{line.BatchId} (محذوفة)";
                sheet.Cell(row, 7).Value = line.BranchName ?? "";
                sheet.Cell(row, 8).Value = line.TargetEntityId;
                sheet.Cell(row, 9).Value = line.StudentsCount;
                sheet.Cell(row, 10).Value = PartnerDispatchKindLabels.DraftStateOf(line.DraftState);
                sheet.Cell(row, 11).Value = line.ContractOverridden ? "نعم" : "لا";
                sheet.Cell(row, 12).Value = line.OverrideReason ?? "";
                sheet.Cell(row, 13).Value = line.SentByName;
                sheet.Cell(row, 14).Value = ToDisplayTime(line.SentAtUtc);
                sheet.Cell(row, 14).Style.DateFormat.Format = "yyyy/MM/dd HH:mm";
                row++;
            }

            if (truncated)
                sheet.Cell(row + 1, 1).Value = $"تم الاكتفاء بأحدث {max:N0} سطر. ضيّق الفلاتر لتصدير الباقي.";

            sheet.Columns(1, headers.Length).AdjustToContents(1, Math.Min(row, 500));
            sheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var fileName = $"سجل_الإرسال_للمدارس_{ToDisplayTime(DateTime.UtcNow):yyyyMMdd_HHmm}.xlsx";
            return new PartnerDispatchLogExport(stream.ToArray(), fileName, truncated);
        }

        // =====================================================
        // الاستعلامات
        // =====================================================
        private IQueryable<PartnerDispatchLog> Filtered(PartnerDispatchLogFilter filter)
        {
            var query = _context.PartnerDispatchLogs.AsNoTracking();

            if (filter.Kind.HasValue && Enum.IsDefined(filter.Kind.Value))
            {
                var kind = filter.Kind.Value;
                query = query.Where(l => l.Kind == kind);
            }

            if (filter.PartnerId is > 0)
            {
                var partnerId = filter.PartnerId.Value;
                query = query.Where(l => l.PartnerId == partnerId);
            }

            if (!string.IsNullOrWhiteSpace(filter.SentByUserId))
            {
                var sender = filter.SentByUserId.Trim();
                query = query.Where(l => l.SentByUserId == sender);
            }

            if (filter.Overridden.HasValue)
            {
                var overridden = filter.Overridden.Value;
                query = query.Where(l => l.ContractOverridden == overridden);
            }

            var (fromUtc, toUtcExclusive) = UtcRange(filter.FromDate, filter.ToDate);
            if (fromUtc.HasValue)
            {
                var from = fromUtc.Value;
                query = query.Where(l => l.SentAtUtc >= from);
            }
            if (toUtcExclusive.HasValue)
            {
                var to = toUtcExclusive.Value;
                query = query.Where(l => l.SentAtUtc < to);
            }

            return query;
        }

        // من/إلى تاريخان بتوقيت السعودية (شاملان). إن انعكسا يُبدَّلان بدل إرجاع نتيجة فارغة.
        public static (DateTime? FromUtc, DateTime? ToUtcExclusive) UtcRange(DateTime? fromLocal, DateTime? toLocal)
        {
            var from = fromLocal?.Date;
            var to = toLocal?.Date;
            if (from.HasValue && to.HasValue && from > to)
                (from, to) = (to, from);

            return (
                from.HasValue ? LocalToUtc(from.Value) : null,
                to.HasValue ? LocalToUtc(to.Value.AddDays(1)) : null);
        }

        private static DateTime LocalToUtc(DateTime local)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), DisplayZone.Value);

        private IQueryable<PartnerDispatchLogLine> ProjectLines(IQueryable<PartnerDispatchLog> logs)
        {
            const PartnerDispatchKind placement = PartnerDispatchKind.Placement;

            // الدفعة بلا مفتاح أجنبي (تبقى في السجل لو حُذفت)، فتُقرأ باستعلامات فرعية مرتبطة في نفس الـ SQL
            return logs.Select(l => new PartnerDispatchLogLine
            {
                Id = l.Id,
                DispatchGroupId = l.DispatchGroupId,
                Kind = l.Kind,
                Title = l.Title,
                PartnerId = l.PartnerId,
                PartnerName = l.Partner != null ? l.Partner.Name : "",
                BatchId = l.BatchId,
                BatchName = _context.Batches.Where(b => b.Id == l.BatchId).Select(b => b.Name).FirstOrDefault(),
                BranchName = _context.Batches.Where(b => b.Id == l.BatchId).Select(b => b.Branch.Name).FirstOrDefault(),
                TargetEntityId = l.TargetEntityId,
                StudentsCount = l.StudentsCount,
                ContractOverridden = l.ContractOverridden,
                OverrideReason = l.OverrideReason,
                SentByUserId = l.SentByUserId,
                SentByName = _context.Users
                    .Where(u => u.Id == l.SentByUserId)
                    .Select(u => u.FullName ?? u.UserName)
                    .FirstOrDefault() ?? l.SentByUserId,
                SentAtUtc = l.SentAtUtc,
                // مسودة تحديد المستوى: صفر طلاب وقت الإنشاء؛ إرسال المدرسة لاحقًا يقلب IsSentToStudents للدفعة
                DraftState = l.Kind == placement && l.StudentsCount == 0
                    ? (_context.ExamAssignmentsToBatches
                        .Any(a => a.ExamId == l.TargetEntityId && a.BatchId == l.BatchId && a.IsSentToStudents)
                        ? PartnerDispatchDraftState.DraftSentByPartner
                        : PartnerDispatchDraftState.DraftPending)
                    : PartnerDispatchDraftState.None
            });
        }

        private static string DisplayName(string? fullName, string? userName, string id)
            => !string.IsNullOrWhiteSpace(fullName) ? fullName! : !string.IsNullOrWhiteSpace(userName) ? userName! : id;
    }
}
