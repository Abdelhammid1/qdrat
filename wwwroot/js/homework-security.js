/**
 * homework-security.js
 * آليات حماية صفحة حل الواجب:
 *   1. منع زر الرجوع في المتصفح
 *   2. حماية الجلسة (استرجاع الحالة من الـ Server)
 *   3. كشف التلاعب الزمني
 *   4. منع فتح الواجب في أكثر من تبويب
 */

const HomeworkSecurity = (() => {

    // ─────────────────────────────────────────
    // Config — تُضبط عند الاستدعاء
    // ─────────────────────────────────────────
    let cfg = {
        homeworkSetId: 0,
        antiForgeryToken: '',
        onBackAttempt: null,      // callback(warningShown)
        onDuplicateTab: null,     // callback()
        onSubmitted: null,        // callback() — الواجب منتهٍ مسبقاً
        maxTimeSuspiciousSecs: 0, // يُعيَّن من الـ Server
    };

    // ─────────────────────────────────────────
    // الحالة الداخلية
    // ─────────────────────────────────────────
    let tabId = crypto.randomUUID?.() ?? Math.random().toString(36).slice(2);
    let broadcastChannel = null;
    let heartbeatTimer = null;
    let isLocked = false; // هل هذا التبويب غير نشط؟

    // ─────────────────────────────────────────
    // 1. منع زر الرجوع
    // ─────────────────────────────────────────
    function initBackButtonPrevention() {
        // أضف حالة وهمية تحلّ محل البداية
        history.pushState({ hwGuard: true }, '', location.href);
        history.pushState({ hwGuard: true }, '', location.href);

        window.addEventListener('popstate', _handlePopState);
    }

    function _handlePopState(e) {
        // أعِد الحالة الوهمية فوراً
        history.pushState({ hwGuard: true }, '', location.href);

        if (typeof cfg.onBackAttempt === 'function') {
            cfg.onBackAttempt();
        } else {
            _showBackWarning();
        }
    }

    function _showBackWarning() {
        const overlay = document.getElementById('hw-back-warning');
        if (overlay) {
            overlay.classList.remove('hw-hidden');
            setTimeout(() => overlay.classList.add('hw-hidden'), 3000);
        }
    }

    // ─────────────────────────────────────────
    // 2. حماية الجلسة — تسجيل البداية مع Server
    // ─────────────────────────────────────────
    async function recordSessionStart() {
        try {
            const res = await fetch('/Students/StudentHomeworkDashboard/RecordSessionStart', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': cfg.antiForgeryToken
                },
                body: JSON.stringify({ homeworkSetId: cfg.homeworkSetId, tabId })
            });
            const data = await res.json();

            if (!data.success && data.isSubmitted) {
                if (typeof cfg.onSubmitted === 'function') cfg.onSubmitted();
                return false;
            }

            if (data.tabId) tabId = data.tabId; // استخدم الـ tabId المُعاد من الـ Server
            return true;
        } catch {
            return false; // لا توقف الواجب عند فشل تسجيل الجلسة
        }
    }

    // ─────────────────────────────────────────
    // 3. كشف التلاعب الزمني
    // ─────────────────────────────────────────
    async function validateQuestionTime(questionId, clientTimeSecs) {
        if (clientTimeSecs <= 0) return;

        try {
            const res = await fetch('/Students/StudentHomeworkDashboard/ValidateQuestionTime', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': cfg.antiForgeryToken
                },
                body: JSON.stringify({
                    homeworkSetId: cfg.homeworkSetId,
                    questionId,
                    clientTimeSecs
                })
            });
            const data = await res.json();

            // data.suspicious is logged server-side only
        } catch {
            // صامت — لا نوقف الواجب بسبب خطأ شبكة
        }
    }

    // ─────────────────────────────────────────
    // 4. منع التبويبات المتعددة (BroadcastChannel)
    // ─────────────────────────────────────────
    function initTabLock() {
        if (!window.BroadcastChannel) return; // المتصفح لا يدعمه

        broadcastChannel = new BroadcastChannel(`hw_tab_${cfg.homeworkSetId}`);

        // أعلن عن فتح هذا التبويب
        broadcastChannel.postMessage({ type: 'tab_open', tabId });

        broadcastChannel.addEventListener('message', _handleTabMessage);

        window.addEventListener('beforeunload', () => {
            broadcastChannel.postMessage({ type: 'tab_close', tabId });
        });

        // Heartbeat — أثبت أن التبويب لا يزال حياً
        heartbeatTimer = setInterval(_sendHeartbeat, 15000);
    }

    function _handleTabMessage(e) {
        const msg = e.data;

        if (msg.type === 'tab_open' && msg.tabId !== tabId) {
            // تبويب آخر فُتح — أخبره بوجودنا ليعرف أنه مكرر
            broadcastChannel.postMessage({ type: 'tab_active', tabId });
            // لا نقفل أنفسنا — نحن الأول
        }

        if (msg.type === 'tab_active' && msg.tabId !== tabId) {
            // تبويب آخر نشط قبلنا
            isLocked = true;
            if (typeof cfg.onDuplicateTab === 'function') {
                cfg.onDuplicateTab();
            } else {
                _showDuplicateWarning();
            }
        }

        if (msg.type === 'tab_close' && msg.tabId !== tabId) {
            // التبويب الآخر أُغلق
            isLocked = false;
            _hideDuplicateWarning();
        }
    }

    async function _sendHeartbeat() {
        if (!tabId) return;
        try {
            await fetch('/Students/StudentHomeworkDashboard/TabHeartbeat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': cfg.antiForgeryToken
                },
                body: JSON.stringify({ homeworkSetId: cfg.homeworkSetId, tabId })
            });
        } catch { /* صامت */ }
    }

    function _showDuplicateWarning() {
        const overlay = document.getElementById('hw-duplicate-warning');
        if (overlay) overlay.classList.remove('hw-hidden');
    }

    function _hideDuplicateWarning() {
        const overlay = document.getElementById('hw-duplicate-warning');
        if (overlay) overlay.classList.add('hw-hidden');
    }

    // ─────────────────────────────────────────
    // Public: تهيئة كل الآليات دفعة واحدة
    // ─────────────────────────────────────────
    async function init(options) {
        cfg = { ...cfg, ...options };

        if (!cfg.homeworkSetId) return;

        // تحقق من الجلسة أولاً — إذا انتهى الواجب أوقف كل شيء
        const sessionOk = await recordSessionStart();
        if (!sessionOk) return;

        initBackButtonPrevention();
        initTabLock();
    }

    // ─────────────────────────────────────────
    // Public: تحقق من الوقت عند إرسال إجابة
    // ─────────────────────────────────────────
    function checkTime(questionId, clientTimeSecs) {
        validateQuestionTime(questionId, clientTimeSecs);
    }

    function isTabLocked() {
        return isLocked;
    }

    function cleanup() {
        window.removeEventListener('popstate', _handlePopState);
        if (heartbeatTimer) clearInterval(heartbeatTimer);
        if (broadcastChannel) broadcastChannel.close();
    }

    return { init, checkTime, isTabLocked, cleanup };

})();
