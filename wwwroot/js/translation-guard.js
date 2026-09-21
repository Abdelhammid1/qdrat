/**
 * translation-guard.js
 * مكوّن حماية مشترك يُستخدم في الستة مسارات: الواجب، الاختبار، المهارات التعزيزية،
 * تحديد المستوى، مؤشر الأداء، معمل القياس.
 *
 * يكتشف تفعيل ترجمة المتصفح (Google Translate الداخلية) أثناء حل المحاولة،
 * يوقف الصفحة فورًا (Overlay غير قابل للإغلاق)، ويُرسل تبليغًا للخادم
 * ليُسجَّل ويُقفَل باب المحاولة حتى يُعيد الأدمن فتحها.
 */
const TranslationGuard = (() => {

    let cfg = {
        attemptType: 0,           // قيمة IntegrityAttemptType الرقمية
        attemptEntityId: 0,
        antiForgeryToken: '',
        reportUrl: '/Students/IntegrityGuard/ReportViolation',
        selfResolveUrl: '/Students/IntegrityGuard/SelfResolve',
        overlayId: 'tg-violation-overlay',
        selfResolveBtnId: 'tg-self-resolve-btn',
        selfResolveMsgId: 'tg-self-resolve-msg',
        locked: false
    };

    function injectNoTranslate() {
        if (!document.querySelector('meta[name="google"]')) {
            const meta = document.createElement('meta');
            meta.name = 'google';
            meta.content = 'notranslate';
            document.head.appendChild(meta);
        }
        document.documentElement.setAttribute('translate', 'no');
        document.documentElement.classList.add('notranslate');
    }

    // Google Translate يضيف class على <html>: translated-ltr / translated-rtl
    // Edge/IE القديم يضيف: _msttexthash
    function isTranslateActive() {
        const html = document.documentElement;
        return html.classList.contains('translated-ltr') ||
               html.classList.contains('translated-rtl') ||
               html.hasAttribute('_msttexthash');
    }

    function initObserver() {
        const html = document.documentElement;
        const check = () => {
            if (cfg.locked) return;
            if (isTranslateActive()) {
                lockPage();
                reportViolation();
            }
        };
        new MutationObserver(check).observe(html, { attributes: true, attributeFilter: ['class'] });
        check(); // فحص فوري لو الترجمة فُعّلت قبل تشغيل الـ observer
    }

    function lockPage() {
        cfg.locked = true;
        const overlay = document.getElementById(cfg.overlayId);
        if (overlay) overlay.classList.remove('tg-hidden');

        document.querySelectorAll('input, button, select, textarea, a').forEach(el => {
            if (overlay && overlay.contains(el)) return;
            el.setAttribute('disabled', 'disabled');
            el.style.pointerEvents = 'none';
        });
    }

    async function reportViolation() {
        let canSelfResolve = false;
        try {
            const res = await fetch(cfg.reportUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': cfg.antiForgeryToken
                },
                body: JSON.stringify({
                    attemptType: cfg.attemptType,
                    attemptEntityId: cfg.attemptEntityId,
                    violationType: 'BrowserTranslate',
                    pageUrl: window.location.href
                })
            });
            const data = await res.json();
            canSelfResolve = !!(data && data.canSelfResolve);
        } catch {
            // صامت — القفل حصل فعليًا على العميل بغض النظر عن نتيجة الشبكة
        }
        updateSelfResolveUI(canSelfResolve);
    }

    function updateSelfResolveUI(canSelfResolve) {
        const btn = document.getElementById(cfg.selfResolveBtnId);
        const msg = document.getElementById(cfg.selfResolveMsgId);
        if (canSelfResolve) {
            if (btn) btn.classList.remove('tg-hidden');
            if (msg) msg.classList.add('tg-hidden');
        } else {
            if (btn) btn.classList.add('tg-hidden');
            if (msg) msg.classList.remove('tg-hidden');
        }
    }

    async function selfResolve() {
        const btn = document.getElementById(cfg.selfResolveBtnId);
        if (btn) {
            btn.setAttribute('disabled', 'disabled');
            btn.textContent = 'جارٍ المعالجة...';
        }
        try {
            const res = await fetch(cfg.selfResolveUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': cfg.antiForgeryToken
                },
                body: JSON.stringify({
                    attemptType: cfg.attemptType,
                    attemptEntityId: cfg.attemptEntityId
                })
            });
            const data = await res.json();
            if (data && data.success) {
                window.location.reload();
                return;
            }
            updateSelfResolveUI(false);
            if (data && data.message) alert(data.message);
        } catch {
            if (btn) {
                btn.removeAttribute('disabled');
                btn.textContent = 'الرجوع مرة واحدة ومتابعة الحل';
            }
        }
    }

    function init(options) {
        cfg = { ...cfg, ...options };
        injectNoTranslate();
        initObserver();
    }

    return { init, selfResolve };

})();
