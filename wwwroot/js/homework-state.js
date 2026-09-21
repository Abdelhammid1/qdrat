/**
 * homework-state.js  v2.0
 * ─────────────────────────────────────────────────────────────────
 * نظام إدارة حالة صفحة الواجب
 *
 *  ┌────────────────────────────────────────────┐
 *  │  الحالة المحلية (RAM)                       │
 *  │  ├── answers   { qId → answerText }         │
 *  │  ├── marks     { qId → true/false }         │
 *  │  ├── times     { qId → seconds }            │
 *  │  └── current   qId (السؤال المعروض الآن)   │
 *  └────────────────────────────────────────────┘
 *
 *  استخدام:
 *    await HWManager.init({ homeworkSetId, allIds, currentId,
 *                           initialAnswers, initialMarks,
 *                           antiForgeryToken });
 *    HWManager.events.on('navigate', fn);
 *    HWManager.events.on('answer',   fn);
 *    HWManager.events.on('mark',     fn);
 */

const HWManager = (() => {

    // ─────────────────────────────────────────
    // داخلي: الحالة الكاملة
    // ─────────────────────────────────────────
    const _st = {
        homeworkSetId:    0,
        allIds:           [],   // [guid, ...]
        currentId:        '',
        answers:          {},   // { guid: answerText }
        marks:            {},   // { guid: bool }
        times:            {},   // { guid: seconds }
        _timerStart:      0,    // Date.now() لبداية السؤال الحالي
        _timerInterval:   null,
        _timerEl:         null,
        _timerDispEl:     null,
        csrf:             '',
        _isBusy:          false,
        _isFinalizing:    false,
        _sessionKey:      '',
        // ── Review Session ──────────────────────
        _review: {
            active:    false,
            enteredAt: null,          // Date object
            navLog:    []             // [{fromId, toId, ts}]
        }
    };

    // ─────────────────────────────────────────
    // بسيط EventEmitter
    // ─────────────────────────────────────────
    const _listeners = {};
    const events = {
        on(name, fn)   { (_listeners[name] ??= []).push(fn); },
        off(name, fn)  { _listeners[name] = (_listeners[name] ?? []).filter(f => f !== fn); },
        emit(name, ...args) { (_listeners[name] ?? []).forEach(f => f(...args)); }
    };

    // ─────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────
    function _ssKey()   { return `hw_state_${_st.homeworkSetId}`; }

    function _ssLoad() {
        try {
            const raw = sessionStorage.getItem(_ssKey());
            return raw ? JSON.parse(raw) : null;
        } catch { return null; }
    }

    function _ssSave() {
        try {
            sessionStorage.setItem(_ssKey(), JSON.stringify({
                answers: _st.answers,
                marks:   _st.marks,
                times:   _st.times,
                current: _st.currentId,
                saved:   Date.now()
            }));
        } catch { /* quota exceeded — صامت */ }
    }

    function _currentIndex() {
        return _st.allIds.indexOf(_st.currentId);
    }

    function _idxOf(id) {
        return _st.allIds.indexOf(id);
    }

    // ─────────────────────────────────────────
    // Timer — ميقات زمني لكل سؤال
    // ─────────────────────────────────────────
    function _timerStart(qId) {
        _timerStop();

        // احسب وقت البداية مع خصم الوقت المحفوظ مسبقاً لنفس السؤال
        const alreadySpent = (_st.times[qId] ?? 0) * 1000;
        _st._timerStart = Date.now() - alreadySpent;

        _st._timerEl    = document.getElementById('hw-timer-val');
        _st._timerDispEl = document.getElementById('hw-timer-display');

        const _circumference = 125.66; // 2π × r(20)
        const _warnSecs      = 120;    // 2 دقيقة حد التحذير

        _st._timerInterval = setInterval(() => {
            const elapsed = Math.floor((Date.now() - _st._timerStart) / 1000);

            // ── نص MM:SS ──
            if (_st._timerEl) {
                const m = String(Math.floor(elapsed / 60)).padStart(2, '0');
                const s = String(elapsed % 60).padStart(2, '0');
                _st._timerEl.textContent = `${m}:${s}`;
            }

            // ── حلقة SVG — تمتلئ خلال 120 ثانية ──
            const ring = document.getElementById('hw-timer-ring');
            if (ring) {
                const progress = Math.min(elapsed / _warnSecs, 1);
                ring.style.strokeDashoffset = String(_circumference * (1 - progress));
            }

            // ── تحذير بعد 2 دقيقة ──
            if (_st._timerDispEl) {
                _st._timerDispEl.classList.toggle('warning', elapsed > _warnSecs);
            }
        }, 500);

        // expose للـ Partial (يستخدمه _hwRestartTimer)
        window.HomeworkState = window.HomeworkState ?? {};
        window.HomeworkState.questionStartTime = _st._timerStart;
    }

    function _timerStop() {
        if (_st._timerInterval) {
            clearInterval(_st._timerInterval);
            _st._timerInterval = null;
        }
    }

    /** احفظ الوقت المستغرق للسؤال الحالي قبل الانتقال */
    function _snapshotCurrentTime() {
        if (!_st.currentId || !_st._timerStart) return;
        const spent = Math.floor((Date.now() - _st._timerStart) / 1000);
        _st.times[_st.currentId] = spent;
    }

    function getTimeTaken(qId) {
        const id = qId ?? _st.currentId;
        if (id === _st.currentId && _st._timerStart) {
            return Math.floor((Date.now() - _st._timerStart) / 1000);
        }
        return _st.times[id] ?? 0;
    }

    // ─────────────────────────────────────────
    // UI — Progress Dots
    // ─────────────────────────────────────────
    function _refreshDots() {
        const dots = document.querySelectorAll('.hw-dot');
        dots.forEach((dot, i) => {
            const qId = _st.allIds[i];
            if (!qId) return;
            dot.classList.remove('answered', 'current', 'marked');
            if (qId === _st.currentId)        dot.classList.add('current');
            else if (_st.marks[qId])           dot.classList.add('marked');
            else if (_st.answers[qId])         dot.classList.add('answered');
        });
    }

    function _refreshReviewGrid() {
        document.querySelectorAll('.hw-review-dot').forEach(btn => {
            const qId = btn.dataset.target;
            if (!qId) return;
            btn.classList.remove('answered', 'unanswered', 'marked', 'current');
            if (qId === _st.currentId)          btn.classList.add('current');
            else if (_st.marks[qId])            btn.classList.add('marked');
            else if (_st.answers[qId])          btn.classList.add('answered');
            else                                btn.classList.add('unanswered');
        });
    }

    function _refreshMarkButton() {
        const btn = document.getElementById('hw-mark-btn');
        if (!btn) return;
        const marked = !!_st.marks[_st.currentId];
        btn.classList.toggle('marked', marked);
    }

    /** استعد الإجابة المحفوظة وحدّد الـ Radio المناسب */
    function _restoreAnswerInDOM() {
        const saved = _st.answers[_st.currentId];
        document.querySelectorAll('#hw-options-list .hw-option').forEach(label => {
            const radio = label.querySelector('input[type="radio"]');
            if (!radio) return;
            const match = saved && radio.value === saved;
            radio.checked = match;
            label.classList.toggle('selected', match);
            label.setAttribute('aria-checked', String(match));
        });
    }

    // ─────────────────────────────────────────
    // Server — fire & forget
    // ─────────────────────────────────────────
    function _serverSave(qId, answer, timeSecs, isMarked, navType = 'stay') {
        const body = JSON.stringify({
            homeworkSetId:    _st.homeworkSetId,
            questionId:       qId,
            selectedAnswer:   answer ?? '',
            timeTakenSeconds: timeSecs,
            isMarkedForReview: !!isMarked
        });

        fetch('/Students/StudentHomeworkDashboard/SaveAnswerJson', {
            method:  'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _st.csrf
            },
            body,
            keepalive: true   // يستمر حتى بعد إغلاق الصفحة
        }).catch(() => { /* صامت */ });
    }

    // ─────────────────────────────────────────
    // init — نقطة البداية
    // ─────────────────────────────────────────
    async function init(opts) {
        _st.homeworkSetId = opts.homeworkSetId ?? 0;
        _st.allIds        = opts.allIds        ?? [];
        _st.currentId     = opts.currentId     ?? _st.allIds[0] ?? '';
        _st.csrf          = opts.antiForgeryToken ?? _getCSRF();

        // ── الأولوية: Server → sessionStorage
        // الـ Server أرسل البيانات في opts (من Model الـ Razor)
        if (opts.initialAnswers && Object.keys(opts.initialAnswers).length > 0) {
            _st.answers = { ...opts.initialAnswers };
            _st.marks   = { ...opts.initialMarks ?? {} };
        } else {
            // fallback: sessionStorage
            const cached = _ssLoad();
            if (cached && cached.answers) {
                _st.answers = cached.answers;
                _st.marks   = cached.marks  ?? {};
                _st.times   = cached.times  ?? {};
            }
        }

        _st.times   = opts.initialTimes ?? _st.times ?? {};

        // ابدأ الميقات
        _timerStart(_st.currentId);

        // ارسم الـ UI
        _refreshDots();
        _refreshMarkButton();
        _restoreAnswerInDOM();

        // expose للـ Partial
        window.HomeworkState = window.HomeworkState ?? {};
        window.HomeworkState.questionStartTime = _st._timerStart;

        // hook لـ _hwRestartTimer المستخدم في الـ Partial
        window._hwRestartTimer = () => {
            _timerStart(_st.currentId);
            _refreshDots();
            _refreshMarkButton();
            _restoreAnswerInDOM();
        };

        events.emit('init', { ..._st });
    }

    // ─────────────────────────────────────────
    // onAnswerChange — تُستدعى عند تغيير الإجابة
    // ─────────────────────────────────────────
    function onAnswerChange(questionId, answer) {
        _st.answers[questionId] = answer;
        _ssSave();

        _serverSave(
            questionId,
            answer,
            getTimeTaken(questionId),
            !!_st.marks[questionId],
            'stay'
        );

        _refreshDots();
        events.emit('answer', { questionId, answer });
    }

    // ─────────────────────────────────────────
    // onMarkReview — تبديل علامة المراجعة
    // ─────────────────────────────────────────
    function onMarkReview(questionId) {
        const id = questionId ?? _st.currentId;
        _st.marks[id] = !_st.marks[id];
        _ssSave();
        _refreshDots();
        _refreshMarkButton();
        _refreshReviewGrid();

        _serverSave(
            id,
            _st.answers[id] ?? '',
            getTimeTaken(id),
            _st.marks[id],
            'review'
        );

        events.emit('mark', { questionId: id, marked: _st.marks[id] });
        return _st.marks[id];
    }

    // ─────────────────────────────────────────
    // beforeNavigate — snapshot قبل الانتقال
    // ─────────────────────────────────────────
    function beforeNavigate(fromId) {
        _snapshotCurrentTime();
        _ssSave();
    }

    // ─────────────────────────────────────────
    // afterNavigate — بعد تحميل السؤال الجديد
    // ─────────────────────────────────────────
    function afterNavigate(newId) {
        _st.currentId = newId;
        _timerStart(newId);
        _refreshDots();
        _refreshReviewGrid();
        _refreshMarkButton();
        _restoreAnswerInDOM();
        _ssSave();
        events.emit('navigate', { questionId: newId });
    }

    // ─────────────────────────────────────────
    // Getters
    // ─────────────────────────────────────────
    function getAnswer(qId)  { return _st.answers[qId ?? _st.currentId] ?? null; }
    function isMarked(qId)   { return !!_st.marks[qId ?? _st.currentId]; }
    function isBusy()        { return _st._isBusy; }
    function setBusy(v)      { _st._isBusy = v; }
    function currentId()     { return _st.currentId; }
    function allIds()        { return [..._st.allIds]; }
    function homeworkSetId() { return _st.homeworkSetId; }
    function csrf()          { return _st.csrf; }

    function getState() {
        return {
            answers:  { ..._st.answers },
            marks:    { ..._st.marks },
            times:    { ..._st.times },
            current:  _st.currentId
        };
    }

    // ─────────────────────────────────────────
    // CSRF helper
    // ─────────────────────────────────────────
    function _getCSRF() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value
            ?? document.querySelector('meta[name="csrf-token"]')?.getAttribute('content')
            ?? '';
    }

    function updateCSRF(token) { _st.csrf = token; }

    // ─────────────────────────────────────────
    // Review Mode
    // ─────────────────────────────────────────
    function enterReviewMode() {
        if (_st._review.active) return;
        _st._review.active    = true;
        _st._review.enteredAt = new Date();
        _st._review.navLog    = [];
        events.emit('reviewEntered', { enteredAt: _st._review.enteredAt });
    }

    function exitReviewMode() {
        _st._review.active = false;
        events.emit('reviewExited', {});
    }

    function logReviewNav(fromId, toId) {
        if (!_st._review.active) return;
        _st._review.navLog.push({ fromId, toId, ts: new Date().toISOString() });
    }

    function getReviewSessionData() {
        return {
            homeworkSetId: _st.homeworkSetId,
            enteredAt:     _st._review.enteredAt?.toISOString() ?? new Date().toISOString(),
            navigationLog: _st._review.navLog.map(e => ({
                questionId: e.toId,
                timestamp:  e.ts
            }))
        };
    }

    function isReviewMode() { return _st._review.active; }

    // ─────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────
    function destroy() {
        _timerStop();
        _ssSave();
    }

    window.addEventListener('beforeunload', () => {
        _snapshotCurrentTime();
        _ssSave();
    });

    // ─────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────
    return {
        init,
        events,
        onAnswerChange,
        onMarkReview,
        beforeNavigate,
        afterNavigate,
        getTimeTaken,
        getAnswer,
        isMarked,
        isBusy,
        setBusy,
        currentId,
        allIds,
        homeworkSetId,
        csrf,
        updateCSRF,
        getState,
        destroy,
        enterReviewMode,
        exitReviewMode,
        logReviewNav,
        getReviewSessionData,
        isReviewMode
    };

})();
