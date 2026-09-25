/* RL-S5 — لوحة طلبات الالتحاق: فلترة الكروت، تحديث الحالة عبر fetch، وتفاصيل الطلب */
(function () {
    'use strict';

    var page = document.getElementById('flPage');
    if (!page || page.dataset.flBound === '1') return;
    page.dataset.flBound = '1';

    var updateUrl = page.dataset.updateUrl;
    var registerUrl = page.dataset.registerUrl || '/register';
    var tokenInput = document.querySelector('#flAntiForgery input[name="__RequestVerificationToken"]');
    var toastHost = document.getElementById('flToast');

    var STATUS = {
        1: { key: 'New', label: 'جديد' },
        2: { key: 'Contacted', label: 'تم التواصل' },
        3: { key: 'NeedFollowUp', label: 'يحتاج متابعة' },
        4: { key: 'Rejected', label: 'مرفوض' },
        5: { key: 'Converted', label: 'مؤكد' }
    };
    var COUNT_KEYS = ['TotalCount', 'NewCount', 'ContactedCount', 'NeedFollowUpCount', 'ConvertedCount', 'RejectedCount', 'TodayCount', 'Last7DaysCount'];

    // ===== بيانات الطلبات للتفاصيل =====
    var leadsById = {};
    var dataEl = document.getElementById('flLeadsData');
    if (dataEl) {
        try {
            JSON.parse(dataEl.textContent).forEach(function (l) { leadsById[l.Id] = l; });
        } catch (e) { /* التفاصيل تتعطل فقط */ }
    }

    // ===== Toast =====
    function toast(message, isError) {
        var el = document.createElement('div');
        el.className = 'fl-toast__item' + (isError ? ' fl-toast__item--error' : '');
        el.textContent = message;
        toastHost.appendChild(el);
        setTimeout(function () { el.remove(); }, 3500);
    }

    // ===== عدّادات الكروت =====
    function applyCounts(counts) {
        if (!counts) return;
        COUNT_KEYS.forEach(function (k) {
            page.querySelectorAll('[data-count="' + k + '"]').forEach(function (n) {
                if (counts[k] !== undefined) n.textContent = counts[k];
            });
        });
        updateShares();
    }

    // نسبة كل حالة من الإجمالي (نص فرعي في الكروت)
    function updateShares() {
        var totalEl = page.querySelector('[data-count="TotalCount"]');
        var total = totalEl ? Number(totalEl.textContent) || 0 : 0;
        page.querySelectorAll('[data-share]').forEach(function (n) {
            var src = page.querySelector('.fl-stat__value[data-count="' + n.dataset.share + '"]');
            var v = src ? Number(src.textContent) || 0 : 0;
            n.textContent = total > 0 ? Math.round(v * 100 / total) + '% من الإجمالي' : '';
        });
    }

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
        var latest = 0;
        page.querySelectorAll('.fl-date__rel').forEach(function (el) {
            var ts = Number(el.dataset.ts);
            if (ts > latest) latest = ts;
            var rel = relative(ts);
            if (rel) el.textContent = rel;
        });
        var header = document.getElementById('flLastLead');
        if (header && latest) header.textContent = 'آخر طلب: ' + (relative(latest) || 'منذ أكثر من أسبوع');
    }

    // ===== نسخ رابط التسجيل =====
    var copyBtn = document.getElementById('flCopyLink');
    if (copyBtn) {
        copyBtn.addEventListener('click', function () {
            var url = window.location.origin + registerUrl;
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(url).then(
                    function () { toast('تم نسخ الرابط'); },
                    function () { toast('تعذّر النسخ، الرابط: ' + url, true); });
            } else {
                toast('الرابط: ' + url);
            }
        });
    }

    // ===== تحديث الحالة (fetch) =====
    function postStatus(id, status, notes) {
        return fetch(updateUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: JSON.stringify({ Id: id, Status: status, AdminNotes: notes })
        }).then(function (res) {
            if (res.status === 404) throw new Error('الطلب غير موجود أو حُذف، حدّث الصفحة');
            if (res.status === 400) throw new Error('تعذّر الحفظ، تحقق من البيانات (الملاحظات حتى 1000 حرف) أو حدّث الصفحة');
            if (!res.ok) throw new Error('حدث خطأ في الخادم، حاول مرة أخرى');
            return res.json();
        });
    }

    var $ = window.jQuery;
    var tableEl = document.getElementById('leadsTable');
    var dt = null;

    // ===== الفلاتر =====
    var filters = { status: page.dataset.initialStatus || '', project: '', days: 0 };

    function rowOf(id) { return tableEl ? tableEl.querySelector('tr[data-id="' + id + '"]') : null; }

    function applyRowStatus(id, status, notes) {
        var tr = rowOf(id);
        var st = STATUS[status];
        if (!tr || !st) return;

        tr.dataset.status = st.key;
        tr.classList.toggle('fl-row--new', st.key === 'New');

        var select = tr.querySelector('.fl-status');
        select.value = String(status);
        select.dataset.status = st.key;
        select.dataset.prev = String(status);

        var td = select.closest('td');
        td.dataset.order = String(status);
        td.dataset.export = st.label;

        if (leadsById[id]) {
            leadsById[id].Status = status;
            if (notes !== undefined) leadsById[id].AdminNotes = notes || null;
        }

        if (dt) dt.row(tr).invalidate().draw(false);
    }

    function changeStatus(id, status, notes, controls) {
        controls.forEach(function (c) { c.disabled = true; });
        return postStatus(id, status, notes).then(function (json) {
            applyRowStatus(id, status, notes);
            applyCounts(json.counts);
            toast('تم تحديث الحالة');
            return json;
        }).finally(function () {
            controls.forEach(function (c) { c.disabled = false; });
        });
    }

    if (tableEl && $ && $.fn.DataTable) {
        $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
            if (settings.nTable !== tableEl) return true;
            var tr = settings.aoData[dataIndex].nTr;
            if (filters.status && tr.dataset.status !== filters.status) return false;
            if (filters.project && tr.dataset.projects.indexOf('|' + filters.project + '|') === -1) return false;
            if (filters.days > 0) {
                var cutoff = filters.days === 1
                    ? new Date(new Date().setHours(0, 0, 0, 0)).getTime()
                    : Date.now() - filters.days * 86400000;
                if (Number(tr.dataset.ts) < cutoff) return false;
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
                    text: '<i class="fas fa-file-excel"></i> تصدير Excel',
                    titleAttr: 'تصدير Excel',
                    title: 'طلبات الالتحاق',
                    className: 'fl-btn-export',
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4],
                        format: {
                            body: function (data, row, column, node) {
                                return node && node.dataset && node.dataset.export !== undefined ? node.dataset.export : data;
                            }
                        }
                    }
                }
            ],
            language: {
                search: 'بحث:',
                lengthMenu: 'عرض _MENU_ صف',
                info: 'عرض _START_ إلى _END_ من _TOTAL_',
                infoEmpty: 'لا توجد طلبات',
                infoFiltered: '(من أصل _MAX_)',
                zeroRecords: '',
                emptyTable: '',
                paginate: { first: 'الأول', last: 'الأخير', next: 'التالي', previous: 'السابق' }
            }
        });

        // التصدير في الهيدر، والترقيم/المعلومات في تذييل الكارت
        var exportHost = document.getElementById('flExportHost');
        if (exportHost) dt.buttons().container().appendTo(exportHost);
        var foot = document.getElementById('flTableFoot');
        if (foot) $(dt.table().container()).find('.dataTables_info, .dataTables_paginate').appendTo(foot);

        dt.on('draw', updateResultUi);
    }

    // ===== البحث =====
    var searchInput = document.getElementById('flSearch');
    var clearBtn = document.getElementById('flClearFilters');
    var resultCount = document.getElementById('flResultCount');
    var noMatch = document.getElementById('flNoMatch');
    var searchTimer = null;

    // تطبيع الأرقام العربية-الهندية إلى لاتينية ليطابق البحث أرقام الجوال
    function normalizeDigits(v) {
        return String(v || '').replace(/[٠-٩]/g, function (d) { return d.charCodeAt(0) - 0x0660; })
            .replace(/[۰-۹]/g, function (d) { return d.charCodeAt(0) - 0x06F0; });
    }

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () {
                if (dt) dt.search(normalizeDigits(searchInput.value.trim())).draw();
            }, 200);
        });
    }

    function hasActiveFilters() {
        return !!(filters.status || filters.project || filters.days > 0 || (searchInput && searchInput.value.trim()));
    }

    function updateResultUi() {
        if (!dt) return;
        var info = dt.page.info();
        if (resultCount) {
            resultCount.textContent = hasActiveFilters()
                ? info.recordsDisplay + ' من ' + info.recordsTotal + ' طلب'
                : info.recordsTotal + ' طلب';
        }
        if (noMatch) noMatch.hidden = info.recordsDisplay !== 0;
        if (clearBtn) clearBtn.hidden = !hasActiveFilters();
    }

    function clearAllFilters() {
        filters.status = ''; filters.project = ''; filters.days = 0;
        if (searchInput) searchInput.value = '';
        if (range) range.value = '0';
        if (dt) dt.search('');
        syncFilterUi();
    }
    if (clearBtn) clearBtn.addEventListener('click', clearAllFilters);
    var noMatchClear = document.getElementById('flNoMatchClear');
    if (noMatchClear) noMatchClear.addEventListener('click', clearAllFilters);

    function syncFilterUi() {
        page.querySelectorAll('.fl-stat').forEach(function (b) {
            b.setAttribute('aria-pressed', String((b.dataset.filterStatus || '') === filters.status && filters.status !== ''));
        });
        page.querySelectorAll('.fl-chip').forEach(function (b) {
            b.setAttribute('aria-pressed', String(b.dataset.filterProject === filters.project && filters.project !== ''));
        });
        if (dt) dt.draw();
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
            filters.project = (btn.dataset.filterProject === filters.project) ? '' : btn.dataset.filterProject;
            syncFilterUi();
        });
    });

    var range = document.getElementById('flRange');
    if (range) {
        range.addEventListener('change', function () {
            filters.days = Number(range.value) || 0;
            syncFilterUi();
        });
    }

    // ===== تغيير الحالة من الجدول (Delegation) =====
    if (tableEl) {
        tableEl.addEventListener('change', function (e) {
            var select = e.target.closest('.fl-status');
            if (!select) return;

            var id = Number(select.closest('tr').dataset.id);
            var prev = Number(select.dataset.prev);
            var next = Number(select.value);
            var notes = leadsById[id] ? leadsById[id].AdminNotes : null;

            changeStatus(id, next, notes, [select]).catch(function (err) {
                select.value = String(prev);
                toast(err.message || 'تعذّر تحديث الحالة', true);
            });
        });
    }

    // ===== تفاصيل الطلب (Offcanvas) =====
    var detailEl = document.getElementById('flDetail');
    var currentId = null;

    function addRow(dl, label, value) {
        if (value === null || value === undefined || value === '') return;
        var dt1 = document.createElement('dt'); dt1.textContent = label;
        var dd = document.createElement('dd'); dd.textContent = value;
        dl.appendChild(dt1); dl.appendChild(dd);
    }

    function fmtDate(iso) {
        if (!iso) return '';
        var d = new Date(iso.endsWith('Z') ? iso : iso + 'Z');
        return d.toLocaleString('ar-SA-u-nu-latn', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Riyadh' });
    }

    function openDetails(id) {
        var l = leadsById[id];
        if (!l || !detailEl) return;
        currentId = id;

        // بطاقة المتقدم
        var isParent = l.ApplicantType === 2;
        document.getElementById('flDetailAvatar').textContent = (l.StudentName || '؟').trim().charAt(0).toUpperCase();
        document.getElementById('flDetailName').textContent = l.StudentName || '';
        var badgeEl = document.getElementById('flDetailBadge');
        badgeEl.textContent = isParent ? 'ولي أمر' : 'طالب';
        badgeEl.className = 'fl-badge ' + (isParent ? 'fl-badge--parent' : 'fl-badge--student');
        document.getElementById('flDetailDate').textContent = fmtDate(l.CreatedAt);

        // أزرار الاتصال السريع (اتصال + واتساب) للمتقدم ولولي الأمر
        var actions = document.getElementById('flDetailActions');
        actions.textContent = '';
        function addAction(cls, href, iconCls, text, external) {
            var a = document.createElement('a');
            a.className = 'fl-btn ' + cls; a.href = href;
            if (external) { a.target = '_blank'; a.rel = 'noopener noreferrer'; }
            var i = document.createElement('i'); i.className = iconCls; i.setAttribute('aria-hidden', 'true');
            a.appendChild(i); a.appendChild(document.createTextNode(text));
            actions.appendChild(a);
        }
        function waNumber(phone) {
            var d = String(phone || '').replace(/\D/g, '');
            if (d.indexOf('966') === 0) return d;
            return d.indexOf('0') === 0 ? '966' + d.substring(1) : '966' + d;
        }
        if (l.PhoneNumber) {
            addAction('fl-btn--soft', 'tel:' + l.PhoneNumber, 'fas fa-phone', 'اتصال');
            addAction('fl-btn--wa', 'https://wa.me/' + waNumber(l.PhoneNumber), 'fab fa-whatsapp', 'واتساب', true);
        }
        if (l.ParentPhone) {
            addAction('fl-btn--soft', 'tel:' + l.ParentPhone, 'fas fa-phone', 'اتصال بولي الأمر');
            addAction('fl-btn--wa', 'https://wa.me/' + waNumber(l.ParentPhone), 'fab fa-whatsapp', 'واتساب ولي الأمر', true);
        }

        var dl = document.getElementById('flDetailInfo');
        dl.textContent = '';
        addRow(dl, 'الجوال', l.PhoneNumber);
        addRow(dl, 'ولي الأمر', l.ParentName);
        addRow(dl, 'جوال ولي الأمر', l.ParentPhone);
        addRow(dl, 'المدينة', l.City);
        addRow(dl, 'المرحلة', l.SchoolStage);
        addRow(dl, 'ملاحظات المتقدم', l.Notes);
        addRow(dl, 'أول تواصل', fmtDate(l.ContactedAt));

        var box = document.getElementById('flDetailCourses');
        box.textContent = '';
        if (l.IsLegacy) {
            var badge = document.createElement('span'); badge.className = 'fl-badge fl-badge--legacy'; badge.textContent = 'طلب قديم';
            var txt = document.createElement('div'); txt.className = 'fl-sub'; txt.textContent = l.LegacyProgram || '';
            box.appendChild(badge); box.appendChild(txt);
        } else if (!l.Courses || l.Courses.length === 0) {
            var none = document.createElement('div'); none.className = 'fl-sub'; none.textContent = 'لم يحدد دورات';
            box.appendChild(none);
        } else {
            var groups = {};
            var order = [];
            l.Courses.forEach(function (c) {
                var k = c.ProjectName || '';
                if (!groups[k]) { groups[k] = []; order.push(k); }
                groups[k].push(c.CourseName);
            });
            order.forEach(function (k) {
                var g = document.createElement('div'); g.className = 'fl-course-group';
                if (k) { var n = document.createElement('span'); n.className = 'fl-course-group__name'; n.textContent = k; g.appendChild(n); }
                groups[k].forEach(function (name) {
                    var chip = document.createElement('span'); chip.className = 'fl-course-chip'; var ic = document.createElement('i'); ic.className = 'fas fa-book-open'; ic.setAttribute('aria-hidden', 'true'); chip.appendChild(ic); chip.appendChild(document.createTextNode(name)); g.appendChild(chip);
                });
                box.appendChild(g);
            });
        }

        document.getElementById('flDetailStatus').value = String(l.Status);
        var notes = document.getElementById('flDetailNotes');
        notes.value = l.AdminNotes || '';
        document.getElementById('flNotesCount').textContent = notes.value.length;
        document.getElementById('flDetailError').style.display = 'none';

        bootstrap.Offcanvas.getOrCreateInstance(detailEl).show();
    }

    if (tableEl) {
        tableEl.addEventListener('click', function (e) {
            var btn = e.target.closest('.fl-details-btn');
            if (btn) openDetails(Number(btn.dataset.id));
        });
    }

    var notesInput = document.getElementById('flDetailNotes');
    if (notesInput) {
        notesInput.addEventListener('input', function () {
            document.getElementById('flNotesCount').textContent = notesInput.value.length;
        });
    }

    var saveBtn = document.getElementById('flDetailSave');
    if (saveBtn) {
        saveBtn.addEventListener('click', function () {
            if (currentId === null) return;
            var spinner = saveBtn.querySelector('.spinner-border');
            var errBox = document.getElementById('flDetailError');
            var status = Number(document.getElementById('flDetailStatus').value);

            errBox.style.display = 'none';
            spinner.classList.remove('d-none');

            changeStatus(currentId, status, notesInput.value, [saveBtn]).then(function () {
                bootstrap.Offcanvas.getOrCreateInstance(detailEl).hide();
            }).catch(function (err) {
                errBox.textContent = err.message || 'تعذّر الحفظ';
                errBox.style.display = 'block';
            }).finally(function () {
                spinner.classList.add('d-none');
            });
        });
    }

    // ===== تهيئة =====
    refreshRelativeTimes();
    updateShares();
    syncFilterUi();
})();
