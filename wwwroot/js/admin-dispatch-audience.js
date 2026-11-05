/*
 * APD-S1.4 — اختيار جمهور الإرسال في شاشات الأدمن (_DispatchAudiencePicker).
 * - اللوحة المخفية تُعطَّل حقولها فلا تُرسل (لا دفعات منصة تحت «الشركاء» والعكس).
 * - قائمة المدارس تُحمَّل مرة عند اختيار «المدارس الشريكة»، وتُعاد عند تغيّر المصدر.
 * - الخادم يتحقق من كل شيء مرة أخرى؛ هذا الملف للواجهة فقط.
 * API: window.QdratDispatchAudience.init(root) / setSource(root, { modelId, curriculumId, courseId })
 */
(function () {
    'use strict';

    var PLATFORM = '1';
    var PARTNERS = '2';

    function qs(root, selector) { return root.querySelector(selector); }
    function qsa(root, selector) { return Array.prototype.slice.call(root.querySelectorAll(selector)); }

    function selectedAudience(root) {
        var checked = qs(root, '[data-apd-audience]:checked');
        return checked ? checked.value : PLATFORM;
    }

    function partnersHost(root) { return qs(root, '[data-apd-partners]'); }

    // ---------- اللوحات ----------
    function applyAudience(root) {
        var audience = selectedAudience(root);

        qsa(root, '[data-apd-panel]').forEach(function (panel) {
            panel.hidden = panel.getAttribute('data-apd-panel') !== audience;
        });

        qsa(root, '[data-apd-platform-batch]').forEach(function (input) {
            input.disabled = audience !== PLATFORM;
        });

        if (audience === PARTNERS) {
            if (root.getAttribute('data-apd-loaded') !== '1') {
                load(root);
            } else {
                syncAllPartners(root);
            }
        } else {
            var host = partnersHost(root);
            if (host) {
                qsa(host, 'input[name], textarea[name]').forEach(function (el) { el.disabled = true; });
            }
        }
    }

    // ---------- التحميل ----------
    function buildUrl(root) {
        var base = root.getAttribute('data-apd-options-url');
        var params = new URLSearchParams();
        params.set('kind', root.getAttribute('data-apd-kind') || '');
        ['model-id:modelId', 'curriculum-id:curriculumId', 'course-id:courseId'].forEach(function (pair) {
            var parts = pair.split(':');
            var value = root.getAttribute('data-apd-' + parts[0]);
            if (value) { params.set(parts[1], value); }
        });
        return base + (base.indexOf('?') >= 0 ? '&' : '?') + params.toString();
    }

    function setState(root, state) {
        var loading = qs(root, '[data-apd-loading]');
        var error = qs(root, '[data-apd-error]');
        if (loading) { loading.hidden = state !== 'loading'; }
        if (error) { error.hidden = state !== 'error'; }
    }

    // المدارس المؤهلة تعتمد على النموذج، فلا يُطلب شيء قبل اختياره
    function needsModel(root) {
        if (root.getAttribute('data-apd-requires-model') !== '1') { return false; }
        var modelId = root.getAttribute('data-apd-model-id');
        return !modelId || modelId === '0';
    }

    // APD-S3.1: دفعات المدارس تتبع كورس الاختبار في الشاشات التي تطلبه
    function needsCourse(root) {
        if (root.getAttribute('data-apd-requires-course') !== '1') { return false; }
        var courseId = root.getAttribute('data-apd-course-id');
        return !courseId || courseId === '0';
    }

    // APD-S4.1: دفعات المدارس تتبع منهج الاختبار في الشاشات التي تطلبه
    function needsCurriculum(root) {
        if (root.getAttribute('data-apd-requires-curriculum') !== '1') { return false; }
        var curriculumId = root.getAttribute('data-apd-curriculum-id');
        return !curriculumId || curriculumId === '0';
    }

    function load(root) {
        var host = partnersHost(root);
        if (!host || root.getAttribute('data-apd-loading-now') === '1') { return; }

        var hint = qs(root, '[data-apd-need-model]');
        if (hint) { hint.hidden = !needsModel(root); }
        var courseHint = qs(root, '[data-apd-need-course]');
        if (courseHint) { courseHint.hidden = needsModel(root) || !needsCourse(root); }
        var curriculumHint = qs(root, '[data-apd-need-curriculum]');
        if (curriculumHint) { curriculumHint.hidden = needsModel(root) || needsCourse(root) || !needsCurriculum(root); }
        if (needsModel(root) || needsCourse(root) || needsCurriculum(root)) {
            setState(root, 'ready');
            updateCounts(root);
            return;
        }

        root.setAttribute('data-apd-loading-now', '1');
        host.innerHTML = '';
        setState(root, 'loading');
        updateCounts(root);

        fetch(buildUrl(root), {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' }
        })
            .then(function (response) {
                if (!response.ok) { throw new Error('HTTP ' + response.status); }
                return response.text();
            })
            .then(function (html) {
                host.innerHTML = html;
                root.setAttribute('data-apd-loaded', '1');
                setState(root, 'ready');
                syncAllPartners(root);
            })
            .catch(function () {
                root.setAttribute('data-apd-loaded', '0');
                setState(root, 'error');
            })
            .then(function () {
                root.removeAttribute('data-apd-loading-now');
            });
    }

    // ---------- المدارس والدفعات ----------
    function overrideOn(root) {
        var check = qs(root, '[data-apd-override-check]');
        return !!(check && check.checked);
    }

    function partnerSelectable(root, li) {
        if (li.getAttribute('data-apd-eligible') === '1') { return true; }
        return li.getAttribute('data-apd-overridable') === '1' && overrideOn(root);
    }

    function partnerHasStudents(li) {
        return qsa(li, '[data-apd-batch]').some(function (b) { return b.getAttribute('data-apd-empty-batch') !== '1'; });
    }

    function syncPartner(root, li) {
        var check = qs(li, '[data-apd-partner-check]');
        if (!check) { return; }

        var selectable = partnerSelectable(root, li) && partnerHasStudents(li);
        check.disabled = !selectable;
        if (!selectable) { check.checked = false; }

        var active = selectedAudience(root) === PARTNERS && check.checked;
        qsa(li, '[data-apd-batch]').forEach(function (batch) {
            batch.disabled = !active || batch.getAttribute('data-apd-empty-batch') === '1';
        });

        var details = qs(li, 'details');
        if (details && active) { details.open = true; }
    }

    function syncOverride(root) {
        var on = overrideOn(root);
        var reasonBox = qs(root, '[data-apd-override-reason]');
        if (reasonBox) {
            reasonBox.hidden = !on;
            var textarea = qs(reasonBox, 'textarea');
            if (textarea) { textarea.disabled = !on || selectedAudience(root) !== PARTNERS; }
        }
        var check = qs(root, '[data-apd-override-check]');
        if (check) { check.disabled = selectedAudience(root) !== PARTNERS; }
    }

    function syncAllPartners(root) {
        qsa(root, '[data-apd-partner]').forEach(function (li) { syncPartner(root, li); });
        syncOverride(root);
        updateCounts(root);
    }

    function updateCounts(root) {
        var partners = 0, batches = 0, students = 0;
        qsa(root, '[data-apd-partner]').forEach(function (li) {
            var check = qs(li, '[data-apd-partner-check]');
            if (!check || !check.checked || check.disabled) { return; }
            var chosen = qsa(li, '[data-apd-batch]:checked').filter(function (b) { return !b.disabled; });
            if (chosen.length === 0) { return; }
            partners++;
            batches += chosen.length;
            chosen.forEach(function (b) { students += parseInt(b.getAttribute('data-apd-students') || '0', 10); });
        });

        var set = function (selector, value) {
            var el = qs(root, selector);
            if (el) { el.textContent = String(value); }
        };
        set('[data-apd-count-partners]', partners);
        set('[data-apd-count-batches]', batches);
        set('[data-apd-count-students]', students);
    }

    function filterPartners(root, term) {
        var needle = (term || '').trim();
        qsa(root, '[data-apd-partner]').forEach(function (li) {
            var name = li.getAttribute('data-apd-name') || '';
            li.hidden = needle.length > 0 && name.indexOf(needle) === -1;
        });
    }

    function selectAll(root, on) {
        qsa(root, '[data-apd-partner]').forEach(function (li) {
            if (li.hidden) { return; }
            var check = qs(li, '[data-apd-partner-check]');
            if (check && !check.disabled) { check.checked = on; }
            syncPartner(root, li);
        });
        updateCounts(root);
    }

    // ---------- الربط ----------
    function bind(root) {
        root.addEventListener('change', function (e) {
            var t = e.target;
            if (t.matches('[data-apd-audience]')) {
                applyAudience(root);
                syncOverride(root);
            } else if (t.matches('[data-apd-partner-check]')) {
                syncPartner(root, t.closest('[data-apd-partner]'));
                updateCounts(root);
            } else if (t.matches('[data-apd-batch]')) {
                updateCounts(root);
            } else if (t.matches('[data-apd-select-all]')) {
                selectAll(root, t.checked);
            } else if (t.matches('[data-apd-override-check]')) {
                syncAllPartners(root);
            }
        });

        root.addEventListener('input', function (e) {
            if (e.target.matches('[data-apd-search]')) { filterPartners(root, e.target.value); }
        });

        root.addEventListener('click', function (e) {
            if (e.target.matches('[data-apd-retry]')) {
                e.preventDefault();
                load(root);
            }
        });

        var modelSelector = root.getAttribute('data-apd-model-input');
        if (modelSelector) {
            var modelInput = document.querySelector(modelSelector);
            if (modelInput) {
                // النموذج قد يكون مختارًا مسبقًا في الصفحة قبل أول تحميل
                if (modelInput.value) { root.setAttribute('data-apd-model-id', modelInput.value); }

                modelInput.addEventListener('change', function () {
                    setSource(root, { modelId: modelInput.value });
                });
            }
        }

        var courseSelector = root.getAttribute('data-apd-course-input');
        if (courseSelector) {
            var courseInput = document.querySelector(courseSelector);
            if (courseInput) {
                if (courseInput.value) { root.setAttribute('data-apd-course-id', courseInput.value); }

                courseInput.addEventListener('change', function () {
                    setSource(root, { courseId: courseInput.value });
                });
            }
        }

        var curriculumSelector = root.getAttribute('data-apd-curriculum-input');
        if (curriculumSelector) {
            var curriculumInput = document.querySelector(curriculumSelector);
            if (curriculumInput) {
                if (curriculumInput.value) { root.setAttribute('data-apd-curriculum-id', curriculumInput.value); }

                curriculumInput.addEventListener('change', function () {
                    setSource(root, { curriculumId: curriculumInput.value });
                });
            }
        }
    }

    function setSource(root, source) {
        source = source || {};
        ['modelId:model-id', 'curriculumId:curriculum-id', 'courseId:course-id'].forEach(function (pair) {
            var parts = pair.split(':');
            if (Object.prototype.hasOwnProperty.call(source, parts[0])) {
                root.setAttribute('data-apd-' + parts[1], source[parts[0]] == null ? '' : String(source[parts[0]]));
            }
        });

        root.setAttribute('data-apd-loaded', '0');
        var host = partnersHost(root);
        if (host) { host.innerHTML = ''; }
        updateCounts(root);

        if (selectedAudience(root) === PARTNERS) { load(root); }
    }

    function init(root) {
        if (!root || root.getAttribute('data-apd-init') === '1') { return; }
        root.setAttribute('data-apd-init', '1');
        bind(root);
        applyAudience(root);
        syncOverride(root);
        updateCounts(root);
    }

    function initAll() {
        qsa(document, '[data-apd-picker]').forEach(init);
    }

    window.QdratDispatchAudience = { init: init, setSource: setSource };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }
})();
