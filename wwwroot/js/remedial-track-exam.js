/* RTK-S5: واجهة حل اختبار 101/102 — حفظ الإجابة عند كل اختيار + مؤقّت من ساعة الخادم + تسليم تلقائي.
   لا يحمل أي مفتاح إجابة؛ كل التصحيح في الخادم. */
(function () {
    'use strict';

    var form = document.getElementById('rtkExamForm');
    if (!form || form.getAttribute('data-bound') === '1') return;   // منع ازدواج الربط
    form.setAttribute('data-bound', '1');

    var saveUrl = form.getAttribute('data-save-url');
    var attemptId = parseInt(form.getAttribute('data-attempt-id'), 10);
    var totalQuestions = parseInt(form.getAttribute('data-total'), 10) || 0;
    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';

    var timerEl = document.getElementById('rtkTimer');
    var stateEl = document.getElementById('rtkSaveState');
    var countEl = document.getElementById('rtkAnsweredCount');
    var submitBtn = document.getElementById('rtkSubmitBtn');

    var submitting = false;
    var expiredOnServer = false;
    var inflight = 0;
    var saved = {};      // questionId -> آخر قيمة محفوظة بنجاح
    var wanted = {};     // questionId -> القيمة المطلوبة حاليًا
    var failed = {};     // questionId -> true إن فشل الحفظ بعد المحاولات

    function setState(text, cls) {
        if (!stateEl) return;
        stateEl.textContent = text || '';
        stateEl.className = 'rtk-save-state' + (cls ? ' ' + cls : '');
    }

    function sections() { return form.querySelectorAll('.rtk-q'); }

    function selectedValue(section) {
        var checked = section.querySelector('input[type="radio"]:checked');
        return checked ? checked.value : null;
    }

    function refreshCount() {
        var n = 0;
        sections().forEach(function (s) { if (selectedValue(s)) n++; });
        if (countEl) countEl.textContent = String(n);
        return n;
    }

    // القيم المحفوظة مسبقًا من الخادم
    sections().forEach(function (s) {
        var id = s.getAttribute('data-question-id');
        var v = selectedValue(s);
        if (v) { saved[id] = v; wanted[id] = v; }
    });
    refreshCount();

    function markSaved(section, ok) {
        var badge = section.querySelector('[data-role="saved"]');
        if (badge) badge.hidden = !ok;
    }

    function delay(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }

    // حفظ إجابة واحدة مع إعادة محاولة بتراجع (3 مرات) لأخطاء الشبكة/الخادم فقط
    function saveOne(section) {
        var id = section.getAttribute('data-question-id');
        var value = selectedValue(section);
        if (!value || saved[id] === value) return Promise.resolve(true);
        wanted[id] = value;

        inflight++;
        setState('جارٍ الحفظ…', 'is-saving');

        function attempt(n) {
            return fetch(saveUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({ attemptId: attemptId, questionId: id, answer: value })
            }).then(function (res) {
                if (res.ok) return { ok: true };
                if (res.status === 409) return { ok: false, final: true, expired: true };
                if (res.status === 400 || res.status === 403 || res.status === 404) return { ok: false, final: true };
                throw new Error('http-' + res.status);   // 429/5xx ← أعد المحاولة
            }).catch(function (err) {
                if (n >= 3) return { ok: false, final: false, error: err };
                return delay(600 * Math.pow(2, n)).then(function () { return attempt(n + 1); });
            });
        }

        return attempt(0).then(function (r) {
            inflight--;
            if (r.ok) {
                saved[id] = value;
                failed[id] = false;
                markSaved(section, true);
                if (inflight === 0) setState('تم حفظ إجاباتك', 'is-ok');
                return true;
            }
            failed[id] = true;
            markSaved(section, false);
            if (r.expired) {
                expiredOnServer = true;
                setState('انتهى وقت الاختبار — جارٍ التسليم…', 'is-error');
                doSubmit(true);
            } else if (r.final) {
                setState('تعذّر حفظ هذه الإجابة. اختر الإجابة مرة أخرى.', 'is-error');
            } else {
                setState('تعذّر الحفظ بسبب الاتصال. تحقق من الإنترنت ثم اختر الإجابة مرة أخرى.', 'is-error');
            }
            return false;
        });
    }

    form.addEventListener('change', function (e) {
        var t = e.target;
        if (!t || t.type !== 'radio') return;
        var section = t.closest ? t.closest('.rtk-q') : null;
        if (!section) return;
        refreshCount();
        saveOne(section);
    });

    // إعادة حفظ ما فشل (قبل التسليم) ثم تسليم النموذج
    function flushAll() {
        var jobs = [];
        sections().forEach(function (s) { jobs.push(saveOne(s)); });
        return Promise.all(jobs);
    }

    function doSubmit(auto) {
        if (submitting) return;
        submitting = true;
        if (submitBtn) { submitBtn.disabled = true; submitBtn.textContent = 'جارٍ التسليم…'; }
        window.removeEventListener('beforeunload', onBeforeUnload);

        var go = function () { form.submit(); };
        if (expiredOnServer) { go(); return; }
        flushAll().then(function (results) {
            var allSaved = results.every(function (r) { return r; });
            if (!allSaved && !auto) {
                // تعذّر حفظ بعض الإجابات: دع الطالب يقرر بدل تسليم ناقص بصمت
                if (!window.confirm('تعذّر حفظ بعض إجاباتك. هل تريد تسليم الاختبار بما تم حفظه؟')) {
                    submitting = false;
                    if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = '<i class="fas fa-paper-plane" aria-hidden="true"></i> تسليم الاختبار'; }
                    window.addEventListener('beforeunload', onBeforeUnload);
                    return;
                }
            }
            go();
        });
    }

    form.addEventListener('submit', function (e) {
        if (submitting && form.getAttribute('data-go') === '1') return;   // التسليم الفعلي
        e.preventDefault();
        if (submitting) return;

        var answered = refreshCount();
        if (!expiredOnServer && answered < totalQuestions) {
            var left = totalQuestions - answered;
            if (!window.confirm('لم تُجب عن ' + left + ' سؤال. هل تريد تسليم الاختبار الآن؟')) return;
        }
        form.setAttribute('data-go', '1');
        doSubmit(false);
    });

    // ───── المؤقّت: من ساعة الخادم، ويُقاس بـ performance.now حتى لا يتأثر بتغيير ساعة الجهاز ─────
    var remainingAtLoad = parseInt(form.getAttribute('data-remaining'), 10);
    if (isNaN(remainingAtLoad)) remainingAtLoad = 0;
    var t0 = (window.performance && performance.now) ? performance.now() : Date.now();
    function nowMs() { return (window.performance && performance.now) ? performance.now() : Date.now(); }

    function fmt(sec) {
        var m = Math.floor(sec / 60), s = sec % 60;
        return (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
    }

    function tick() {
        var left = Math.max(0, remainingAtLoad - Math.floor((nowMs() - t0) / 1000));
        if (timerEl) {
            timerEl.textContent = fmt(left);
            timerEl.classList.toggle('is-warning', left <= 60);
        }
        if (left <= 0 && !submitting) {
            setState('انتهى الوقت — جارٍ تسليم الاختبار…', 'is-error');
            form.setAttribute('data-go', '1');
            doSubmit(true);
        }
    }
    tick();
    setInterval(tick, 1000);

    // ───── تحذير المغادرة ─────
    function onBeforeUnload(e) {
        if (submitting) return undefined;
        e.preventDefault();
        e.returnValue = '';
        return '';
    }
    window.addEventListener('beforeunload', onBeforeUnload);
})();
