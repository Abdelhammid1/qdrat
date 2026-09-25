/* طلبات تسجيل الرعاة: كروت تفلتر الجدول مباشرة + بحث + فلاتر الشركة والفترة + تصدير النتائج */
(function () {
    'use strict';

    var page = document.getElementById('crPage');
    if (!page || page.dataset.crBound === '1') return;
    page.dataset.crBound = '1';

    var $ = window.jQuery;
    var tableEl = document.getElementById('requestsTable');
    if (!tableEl || !$ || !$.fn.DataTable) return;

    var filters = { status: '', company: '', days: 0 };
    var searchInput = document.getElementById('crSearch');
    var range = document.getElementById('crRange');
    var clearBtn = document.getElementById('crClearFilters');
    var resultCount = document.getElementById('crResultCount');
    var noMatch = document.getElementById('crNoMatch');
    var dt = null;

    // ===== وقت نسبي =====
    function relative(ts) {
        var mins = Math.floor((Date.now() - ts) / 60000);
        if (mins < 1) return 'الآن';
        if (mins < 60) return 'منذ ' + mins + ' دقيقة';
        var hours = Math.floor(mins / 60);
        if (hours < 24) return 'منذ ' + hours + ' ساعة';
        var days = Math.floor(hours / 24);
        return days < 7 ? 'منذ ' + days + ' يوم' : null;
    }

    function refreshRelativeTimes() {
        page.querySelectorAll('.fl-date__rel').forEach(function (el) {
            var rel = relative(new Date(el.dataset.date).getTime());
            if (rel) el.textContent = rel;
        });
        var header = document.getElementById('crLastRequest');
        if (header && page.dataset.last) {
            header.textContent = 'آخر طلب: ' + (relative(new Date(page.dataset.last).getTime()) || 'منذ أكثر من أسبوع');
        }
    }

    // تطبيع الأرقام العربية-الهندية إلى لاتينية ليطابق البحث الجوال والهوية
    function normalizeDigits(v) {
        return String(v || '').replace(/[٠-٩]/g, function (d) { return d.charCodeAt(0) - 0x0660; })
            .replace(/[۰-۹]/g, function (d) { return d.charCodeAt(0) - 0x06F0; });
    }

    // ===== فلتر مخصص: الحالة + الشركة + الفترة =====
    $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
        if (settings.nTable !== tableEl) return true;
        var tr = settings.aoData[dataIndex].nTr;
        if (filters.status && tr.dataset.status !== filters.status) return false;
        if (filters.company && tr.dataset.company !== filters.company) return false;
        if (filters.days > 0) {
            var cutoff = filters.days === 1
                ? new Date(new Date().setHours(0, 0, 0, 0)).getTime()
                : Date.now() - filters.days * 86400000;
            if (new Date(tr.dataset.date).getTime() < cutoff) return false;
        }
        return true;
    });

    dt = $(tableEl).DataTable({
        pageLength: 10,
        order: [[3, 'desc']],
        columnDefs: [{ orderable: false, targets: [1, 2, 5] }],
        dom: 'rtip',
        buttons: [
            {
                extend: 'excelHtml5',
                text: '<i class="fas fa-file-excel"></i> تصدير النتائج',
                titleAttr: 'تصدير الصفوف المعروضة حاليًا',
                title: 'طلبات تسجيل الرعاة',
                exportOptions: {
                    columns: [0, 1, 2, 3, 4],
                    modifier: { search: 'applied' },
                    format: {
                        body: function (data, row, column, node) {
                            return node && node.dataset && node.dataset.export !== undefined ? node.dataset.export : data;
                        }
                    }
                }
            }
        ],
        language: {
            lengthMenu: 'عرض _MENU_ صف',
            info: 'عرض _START_ إلى _END_ من _TOTAL_',
            infoEmpty: 'لا توجد طلبات',
            infoFiltered: '(من أصل _MAX_)',
            zeroRecords: '',
            emptyTable: '',
            paginate: { first: 'الأول', last: 'الأخير', next: 'التالي', previous: 'السابق' }
        }
    });

    var exportHost = document.getElementById('crExportHost');
    if (exportHost) dt.buttons().container().appendTo(exportHost);
    var foot = document.getElementById('crTableFoot');
    if (foot) $(dt.table().container()).find('.dataTables_info, .dataTables_paginate').appendTo(foot);

    // ===== حالة الواجهة =====
    function hasActiveFilters() {
        return !!(filters.status || filters.company || filters.days > 0 || (searchInput && searchInput.value.trim()));
    }

    function updateResultUi() {
        var info = dt.page.info();
        if (resultCount) {
            resultCount.textContent = hasActiveFilters()
                ? info.recordsDisplay + ' من ' + info.recordsTotal + ' طلب'
                : info.recordsTotal + ' طلب';
        }
        if (noMatch) noMatch.hidden = info.recordsDisplay !== 0;
        if (clearBtn) clearBtn.hidden = !hasActiveFilters();
    }
    dt.on('draw', updateResultUi);

    function syncFilterUi() {
        page.querySelectorAll('.fl-stat').forEach(function (b) {
            var v = b.dataset.filterStatus || '';
            b.setAttribute('aria-pressed', String(v !== '' && v === filters.status));
        });
        page.querySelectorAll('.fl-chip').forEach(function (b) {
            b.setAttribute('aria-pressed', String(filters.company !== '' && b.dataset.filterCompany === filters.company));
        });
        dt.draw();
    }

    function clearAllFilters() {
        filters.status = ''; filters.company = ''; filters.days = 0;
        if (searchInput) searchInput.value = '';
        if (range) range.value = '0';
        dt.search('');
        syncFilterUi();
    }

    // ===== الأحداث (ربط مرة واحدة) =====
    var searchTimer = null;
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () {
                dt.search(normalizeDigits(searchInput.value.trim())).draw();
            }, 200);
        });
    }

    page.querySelectorAll('.fl-stat').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var next = btn.dataset.filterStatus || '';
            filters.status = (next === filters.status) ? '' : next;
            syncFilterUi();
        });
    });

    page.querySelectorAll('.fl-chip').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var next = btn.dataset.filterCompany;
            filters.company = (next === filters.company) ? '' : next;
            syncFilterUi();
        });
    });

    if (range) {
        range.addEventListener('change', function () {
            filters.days = Number(range.value) || 0;
            syncFilterUi();
        });
    }

    if (clearBtn) clearBtn.addEventListener('click', clearAllFilters);
    var noMatchClear = document.getElementById('crNoMatchClear');
    if (noMatchClear) noMatchClear.addEventListener('click', clearAllFilters);

    refreshRelativeTimes();
    updateResultUi();
})();
