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

    // ───── التنقل سؤالًا بسؤال + لوحة المراجعة (نفس أسلوب الاختبارات العامة) ─────
    var shell = document.getElementById('rtkShell');
    var navBar = document.getElementById('rtkNavBar');
    var reviewTrigger = document.getElementById('rtkReviewTrigger');
    var reviewPanel = document.getElementById('rtkReviewPanel');
    var reviewGrid = document.getElementById('rtkReviewGrid');
    var prevBtn = form.querySelector('[data-rtk-nav="prev"]');
    var nextBtn = form.querySelector('[data-rtk-nav="next"]');
    var flagBtn = form.querySelector('[data-rtk-nav="flag"]');
    var flagLabel = flagBtn ? flagBtn.querySelector('[data-role="flag-label"]') : null;

    var currentIndex = 1;
    var inReview = false;
    var flagged = {};    // questionId -> true (علامة مراجعة؛ تُحفظ في المتصفح فقط)
    var flagKey = 'rtk_flags_' + attemptId;

    try {
        var rawFlags = window.sessionStorage.getItem(flagKey);
        if (rawFlags) flagged = JSON.parse(rawFlags) || {};
    } catch (e) { flagged = {}; }

    function persistFlags() {
        try { window.sessionStorage.setItem(flagKey, JSON.stringify(flagged)); } catch (e) { /* التخزين غير متاح */ }
    }

    function sectionAt(index) { return form.querySelector('.rtk-q[data-index="' + index + '"]'); }
    function sectionId(section) { return section.getAttribute('data-question-id'); }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) el.textContent = String(value);
    }

    function renderReview() {
        var answered = 0, marked = 0, unanswered = 0;
        sections().forEach(function (s) {
            var idx = s.getAttribute('data-index');
            var isAns = !!selectedValue(s);
            var isMark = !!flagged[sectionId(s)];
            if (isMark) marked++; else if (isAns) answered++; else unanswered++;

            var btn = reviewGrid ? reviewGrid.querySelector('[data-rtk-goto="' + idx + '"]') : null;
            if (!btn) return;
            btn.classList.remove('btn-warning', 'btn-success', 'btn-outline-secondary',
                'qx-qbtn--flagged', 'qx-qbtn--answered', 'qx-qbtn--unanswered');
            if (isMark) btn.classList.add('btn-warning', 'qx-qbtn--flagged');
            else if (isAns) btn.classList.add('btn-success', 'qx-qbtn--answered');
            else btn.classList.add('btn-outline-secondary', 'qx-qbtn--unanswered');
            var isCurr = parseInt(idx, 10) === currentIndex;
            btn.classList.toggle('review-btn--current', isCurr);
            btn.classList.toggle('qx-qbtn--current', isCurr);
        });

        var withAnswer = answered + marked;
        var pct = totalQuestions > 0 ? Math.round(withAnswer / totalQuestions * 100) : 0;
        setText('rtkStatAnswered', answered);
        setText('rtkStatFlagged', marked);
        setText('rtkStatUnanswered', unanswered);
        setText('rtkProgressLabel', withAnswer + ' / ' + totalQuestions);
        var fill = document.getElementById('rtkProgressFill');
        if (fill) fill.style.width = pct + '%';
        return { answered: answered, marked: marked, unanswered: unanswered };
    }

    function renderNav() {
        var onLast = currentIndex >= totalQuestions;
        if (prevBtn) prevBtn.disabled = currentIndex <= 1;
        // آخر سؤال ← زر «مراجعة الأسئلة» بدل شريط التنقل (كما في الاختبارات العامة)
        if (navBar) navBar.style.display = (inReview || onLast) ? 'none' : '';
        if (reviewTrigger) reviewTrigger.hidden = inReview || !onLast;

        var cur = sectionAt(currentIndex);
        var isMark = cur ? !!flagged[sectionId(cur)] : false;
        if (flagBtn) flagBtn.setAttribute('aria-pressed', isMark ? 'true' : 'false');
        if (flagLabel) flagLabel.textContent = isMark ? 'إزالة علامة المراجعة' : 'ضع علامة للمراجعة';
    }

    function showQuestion(index) {
        if (index < 1 || index > totalQuestions) return;
        currentIndex = index;
        inReview = false;
        sections().forEach(function (s) {
            s.hidden = parseInt(s.getAttribute('data-index'), 10) !== index;
        });
        if (reviewPanel) reviewPanel.hidden = true;
        if (shell) shell.classList.remove('exam-in-review-mode');
        renderNav();
        renderReview();
        if (typeof window.syncOptionStyles === 'function') window.syncOptionStyles(form);
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function showReview() {
        inReview = true;
        sections().forEach(function (s) { s.hidden = true; });
        if (reviewPanel) reviewPanel.hidden = false;
        if (shell) shell.classList.add('exam-in-review-mode');
        renderNav();
        renderReview();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function toggleFlag() {
        var cur = sectionAt(currentIndex);
        if (!cur) return;
        var id = sectionId(cur);
        if (flagged[id]) delete flagged[id]; else flagged[id] = true;
        persistFlags();
        renderNav();
        renderReview();
    }

    form.addEventListener('click', function (e) {
        var t = e.target;
        if (!t || !t.closest) return;
        var navEl = t.closest('[data-rtk-nav]');
        if (navEl) {
            var nav = navEl.getAttribute('data-rtk-nav');
            if (nav === 'prev') showQuestion(currentIndex - 1);
            else if (nav === 'next') showQuestion(currentIndex + 1);
            else if (nav === 'flag') toggleFlag();
            else if (nav === 'showReview') showReview();
            return;
        }
        var jump = t.closest('[data-rtk-goto]');
        if (jump) showQuestion(parseInt(jump.getAttribute('data-rtk-goto'), 10));
    });

    function refreshCount() {
        var n = 0;
        sections().forEach(function (s) { if (selectedValue(s)) n++; });
        if (countEl) countEl.textContent = String(n);
        renderReview();
        return n;
    }

    // القيم المحفوظة مسبقًا من الخادم
    sections().forEach(function (s) {
        var id = s.getAttribute('data-question-id');
        var v = selectedValue(s);
        if (v) { saved[id] = v; wanted[id] = v; }
    });
    refreshCount();
    renderNav();

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
                if (res.status === 400 || res.status === 403 || res.status === 404) {
                    // اقرأ سبب الرفض من الخادم ليظهر للطالب/للدعم بدل رسالة عامة
                    return res.json().catch(function () { return null; }).then(function (b) {
                        return { ok: false, final: true, status: res.status, message: b && b.message };
                    });
                }
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
                setState('تعذّر حفظ هذه الإجابة. اختر الإجابة مرة أخرى.'
                    + (r.message ? ' (' + r.message + ')' : (r.status ? ' (' + r.status + ')' : '')), 'is-error');
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
        var proceed = function () {
            form.setAttribute('data-go', '1');
            doSubmit(false);
        };
        if (expiredOnServer) { proceed(); return; }

        // الأعداد الفعلية للإجابات (السؤال الموسوم قد يكون مجابًا أو لا)
        var stats = { marked: Object.keys(flagged).length, unanswered: totalQuestions - answered };
        var answeredAll = answered;

        if (typeof Swal === 'undefined') {
            var left = totalQuestions - answered;
            if (left > 0 && !window.confirm('لم تُجب عن ' + left + ' سؤال. هل تريد تسليم الاختبار الآن؟')) return;
            proceed();
            return;
        }

        Swal.fire({
            title: 'هل أنت متأكد من إنهاء الاختبار؟',
            html: '<div dir="rtl" style="display:flex;flex-direction:column;gap:.5rem;text-align:right">'
                + '<div style="background:#e8f5e9;border-radius:10px;padding:.5rem 1rem"><b style="color:#2e7d32;font-size:1.4rem">' + answeredAll + '</b> سؤال أجبت عليه</div>'
                + '<div style="background:#ffeaea;border-radius:10px;padding:.5rem 1rem"><b style="color:#c62828;font-size:1.4rem">' + stats.unanswered + '</b> سؤال لم تُجب عليه</div>'
                + '<div style="background:#fff8e1;border-radius:10px;padding:.5rem 1rem"><b style="color:#e65100;font-size:1.4rem">' + stats.marked + '</b> سؤال بعلامة مراجعة</div>'
                + '</div>',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'إنهاء نهائيًا',
            cancelButtonText: 'العودة للاختبار',
            confirmButtonColor: '#d33',
            cancelButtonColor: '#3085d6'
        }).then(function (result) {
            if (result.isConfirmed) proceed();
        });
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
