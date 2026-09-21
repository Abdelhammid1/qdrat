/**
 * homework-review.js  v1.0
 * ─────────────────────────────────────────────────────────────────
 * لوحة مراجعة الأسئلة — Bottom-sheet overlay
 *
 *  يُستدعى من Start.cshtml:
 *    ReviewMode.open()          — يفتح اللوحة
 *    ReviewMode.close()         — يغلق اللوحة
 *    ReviewMode.isOpen()        — حالة اللوحة
 *
 *  يستمع لأحداث HWManager:
 *    navigate / answer / mark   — يحدّث الشبكة تلقائياً
 */

const ReviewMode = (() => {

    // ─────────────────────────────────────────
    // الحالة الداخلية
    // ─────────────────────────────────────────
    let _open       = false;
    let _panel      = null;   // العنصر الجذر للوحة
    let _onNavigate = null;   // callback للانتقال

    // ─────────────────────────────────────────
    // CSS — حقن مرة واحدة عند أول استخدام
    // ─────────────────────────────────────────
    function _injectStyles() {
        if (document.getElementById('hw-review-styles')) return;

        const style = document.createElement('style');
        style.id = 'hw-review-styles';
        style.textContent = `
/* ── Review Panel ─────────────────────────────────── */
:root {
    --rv-answered:  #16a34a;   /* أخضر */
    --rv-marked:    #d97706;   /* ذهبي/برتقالي */
    --rv-unanswered:#94a3b8;   /* رمادي */
    --rv-current:   #4f46e5;   /* بنفسجي */
    --rv-bg:        rgba(15,23,42,.55);
    --rv-sheet-bg:  #fff;
    --rv-radius:    1.25rem;
}

#hw-review-panel {
    position: fixed;
    inset: 0;
    z-index: 9000;
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
    background: var(--rv-bg);
    backdrop-filter: blur(4px);
    -webkit-backdrop-filter: blur(4px);
    opacity: 0;
    pointer-events: none;
    transition: opacity .25s ease;
}
#hw-review-panel.open {
    opacity: 1;
    pointer-events: auto;
}

.rv-sheet {
    background: var(--rv-sheet-bg);
    border-radius: var(--rv-radius) var(--rv-radius) 0 0;
    padding: 1.25rem 1.25rem 2rem;
    max-height: 82vh;
    overflow-y: auto;
    overscroll-behavior: contain;
    transform: translateY(100%);
    transition: transform .3s cubic-bezier(.4,0,.2,1);
    direction: rtl;
}
#hw-review-panel.open .rv-sheet {
    transform: translateY(0);
}

/* Handle bar */
.rv-handle {
    width: 3rem;
    height: .35rem;
    background: #cbd5e1;
    border-radius: 99px;
    margin: 0 auto .75rem;
}

/* Header */
.rv-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 1rem;
}
.rv-title {
    font-size: 1.05rem;
    font-weight: 700;
    color: #1e293b;
    margin: 0;
}
.rv-close-btn {
    width: 2rem;
    height: 2rem;
    border: none;
    background: #f1f5f9;
    border-radius: 50%;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1rem;
    color: #64748b;
    transition: background .15s;
}
.rv-close-btn:hover { background: #e2e8f0; }

/* Legend */
.rv-legend {
    display: flex;
    gap: .75rem;
    flex-wrap: wrap;
    margin-bottom: 1rem;
    font-size: .7rem;
    color: #475569;
}
.rv-legend-item {
    display: flex;
    align-items: center;
    gap: .3rem;
}
.rv-legend-dot {
    width: .75rem;
    height: .75rem;
    border-radius: 50%;
    flex-shrink: 0;
}

/* Stats bar */
.rv-stats {
    display: flex;
    gap: .5rem;
    flex-wrap: wrap;
    margin-bottom: 1rem;
}
.rv-stat-chip {
    padding: .2rem .6rem;
    border-radius: 99px;
    font-size: .72rem;
    font-weight: 600;
    color: #fff;
}
.rv-stat-chip.answered  { background: var(--rv-answered); }
.rv-stat-chip.marked    { background: var(--rv-marked); }
.rv-stat-chip.unanswered{ background: var(--rv-unanswered); color: #1e293b; }

/* Question grid */
.rv-grid {
    display: flex;
    flex-wrap: wrap;
    gap: .55rem;
    margin-bottom: 1.5rem;
}
.hw-review-dot {
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 50%;
    border: 2px solid transparent;
    font-size: .82rem;
    font-weight: 700;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: transform .12s, box-shadow .12s;
    color: #fff;
    background: var(--rv-unanswered);
    outline: none;
}
.hw-review-dot:hover   { transform: scale(1.12); box-shadow: 0 2px 8px rgba(0,0,0,.18); }
.hw-review-dot:focus-visible { box-shadow: 0 0 0 3px var(--rv-current); }
.hw-review-dot.answered  { background: var(--rv-answered); }
.hw-review-dot.marked    { background: var(--rv-marked); }
.hw-review-dot.unanswered{ background: var(--rv-unanswered); color: #1e293b; }
.hw-review-dot.current   {
    background: var(--rv-current);
    border-color: #312e81;
    box-shadow: 0 0 0 3px rgba(79,70,229,.3);
}

/* Submit button */
.rv-submit-btn {
    width: 100%;
    padding: .85rem;
    background: linear-gradient(135deg, #16a34a, #15803d);
    color: #fff;
    border: none;
    border-radius: .75rem;
    font-size: 1rem;
    font-weight: 700;
    cursor: pointer;
    transition: opacity .15s, transform .12s;
    letter-spacing: .01em;
}
.rv-submit-btn:hover  { opacity: .92; transform: translateY(-1px); }
.rv-submit-btn:active { transform: translateY(0); }

/* Dark mode */
@media (prefers-color-scheme: dark) {
    :root {
        --rv-sheet-bg: #1e293b;
    }
    .rv-title  { color: #f1f5f9; }
    .rv-legend { color: #94a3b8; }
    .rv-close-btn { background: #334155; color: #cbd5e1; }
    .rv-close-btn:hover { background: #475569; }
}
        `;
        document.head.appendChild(style);
    }

    // ─────────────────────────────────────────
    // بناء HTML اللوحة
    // ─────────────────────────────────────────
    function _buildPanel() {
        if (_panel) return;

        _injectStyles();

        _panel = document.createElement('div');
        _panel.id = 'hw-review-panel';
        _panel.setAttribute('role', 'dialog');
        _panel.setAttribute('aria-modal', 'true');
        _panel.setAttribute('aria-label', 'مراجعة الأسئلة');

        _panel.innerHTML = `
<div class="rv-sheet" id="rv-sheet">
    <div class="rv-handle"></div>

    <div class="rv-header">
        <h2 class="rv-title">مراجعة الأسئلة</h2>
        <button class="rv-close-btn" id="rv-close-btn" aria-label="إغلاق">✕</button>
    </div>

    <div class="rv-legend">
        <span class="rv-legend-item">
            <span class="rv-legend-dot" style="background:var(--rv-answered)"></span> أُجيب عليه
        </span>
        <span class="rv-legend-item">
            <span class="rv-legend-dot" style="background:var(--rv-marked)"></span> أُجيب + علامة مراجعة
        </span>
        <span class="rv-legend-item">
            <span class="rv-legend-dot" style="background:var(--rv-unanswered)"></span> لم يُجب عليه
        </span>
        <span class="rv-legend-item">
            <span class="rv-legend-dot" style="background:var(--rv-current);border:2px solid #312e81"></span> السؤال الحالي
        </span>
    </div>

    <div class="rv-stats" id="rv-stats"></div>

    <div class="rv-grid" id="rv-grid" role="group" aria-label="الأسئلة"></div>

    <button class="rv-submit-btn" id="rv-submit-btn">إنهاء الواجب وتسليمه</button>
</div>`;

        document.body.appendChild(_panel);

        // ── إغلاق بالضغط على الخلفية
        _panel.addEventListener('click', e => {
            if (e.target === _panel) close();
        });

        // ── إغلاق بزر X
        document.getElementById('rv-close-btn').addEventListener('click', () => close());

        // ── Keyboard: Escape
        document.addEventListener('keydown', _onKeydown);

        // ── أزرار الشبكة (delegation)
        document.getElementById('rv-grid').addEventListener('click', e => {
            const btn = e.target.closest('.hw-review-dot');
            if (!btn) return;
            const targetId = btn.dataset.target;
            if (!targetId) return;
            _navigateTo(targetId);
        });

        // ── زر التسليم
        document.getElementById('rv-submit-btn').addEventListener('click', () => {
            _triggerSubmit();
        });
    }

    // ─────────────────────────────────────────
    // تحديث الشبكة
    // ─────────────────────────────────────────
    function _rebuildGrid() {
        if (!_panel) return;

        const grid    = document.getElementById('rv-grid');
        const stats   = document.getElementById('rv-stats');
        const allIds  = HWManager.allIds();
        const state   = HWManager.getState();
        const curId   = HWManager.currentId();

        if (!grid) return;

        let answered  = 0;
        let markedCount = 0;
        let unanswered = 0;

        grid.innerHTML = '';

        allIds.forEach((qId, idx) => {
            const hasAnswer = !!state.answers[qId];
            const isMarked  = !!state.marks[qId];
            const isCurrent = qId === curId;

            if (isMarked)       markedCount++;
            else if (hasAnswer) answered++;
            else                unanswered++;

            const btn = document.createElement('button');
            btn.className   = 'hw-review-dot';
            btn.dataset.target = qId;
            btn.textContent = String(idx + 1);
            btn.setAttribute('aria-label', `السؤال ${idx + 1}`);

            if (isCurrent)      btn.classList.add('current');
            else if (isMarked)  btn.classList.add('marked');
            else if (hasAnswer) btn.classList.add('answered');
            else                btn.classList.add('unanswered');

            grid.appendChild(btn);
        });

        // تحديث الإحصاء
        if (stats) {
            stats.innerHTML = `
                <span class="rv-stat-chip answered">${answered + markedCount} مُجاب</span>
                <span class="rv-stat-chip marked">${markedCount} للمراجعة</span>
                <span class="rv-stat-chip unanswered">${unanswered} لم يُجب</span>
            `;
        }
    }

    // ─────────────────────────────────────────
    // التنقل من اللوحة → سؤال محدد
    // ─────────────────────────────────────────
    function _navigateTo(targetId) {
        const currentId = HWManager.currentId();

        // سجّل في التنقل
        HWManager.logReviewNav(currentId, targetId);

        // أغلق اللوحة
        close();

        // فجّر callback التنقل (Start.cshtml يربطه)
        if (typeof _onNavigate === 'function') {
            _onNavigate(targetId);
        }
    }

    // ─────────────────────────────────────────
    // التسليم النهائي من اللوحة
    // ─────────────────────────────────────────
    function _triggerSubmit() {
        // نبعث event يستمع له Start.cshtml
        document.dispatchEvent(new CustomEvent('hw:submitRequest', { detail: { fromReview: true } }));
    }

    // ─────────────────────────────────────────
    // Keyboard handler
    // ─────────────────────────────────────────
    function _onKeydown(e) {
        if (!_open) return;
        if (e.key === 'Escape') close();
    }

    // ─────────────────────────────────────────
    // فتح / إغلاق
    // ─────────────────────────────────────────
    function open(opts = {}) {
        _onNavigate = opts.onNavigate ?? null;

        _buildPanel();
        HWManager.enterReviewMode();
        _rebuildGrid();

        // أظهر اللوحة
        requestAnimationFrame(() => {
            _panel.classList.add('open');
            _open = true;

            // focus على أول زر في الشبكة لإمكانية الوصول
            const firstBtn = document.querySelector('.hw-review-dot');
            if (firstBtn) firstBtn.focus();
        });
    }

    function close() {
        if (!_panel) return;
        _panel.classList.remove('open');
        _open = false;
        // وضع المراجعة يبقى نشطاً حتى التسليم لتتبع سجل التنقل
    }

    function destroy() {
        document.removeEventListener('keydown', _onKeydown);
        if (_panel) { _panel.remove(); _panel = null; }
        _open = false;
    }

    function isOpen() { return _open; }

    // ─────────────────────────────────────────
    // تحديث تلقائي عند تغيير الإجابة/العلامة/التنقل
    // ─────────────────────────────────────────
    function _wireHWManagerEvents() {
        HWManager.events.on('answer',   () => { if (_open) _rebuildGrid(); });
        HWManager.events.on('mark',     () => { if (_open) _rebuildGrid(); });
        HWManager.events.on('navigate', () => { if (_open) _rebuildGrid(); });
    }

    // ─────────────────────────────────────────
    // تهيئة
    // ─────────────────────────────────────────
    function init() {
        _injectStyles();
        _wireHWManagerEvents();
    }

    // ─────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────
    return { init, open, close, isOpen, destroy };

})();
