/* OLC-S6 — المشغّل المحمي للمحتوى التعليمي أونلاين (معيار C.7). منقول من remedial-track-player.js (دون تعديله):
 * - P2 طبقة حماية · P3 أدوات تحكم خاصة · P4 لا تقديم مع السماح بالرجوع · P8 رادع الكليك يمين وأدوات المطوّر · P9 ملء الشاشة على الحاوية.
 * - P10 نبضة كل 15ث أثناء التشغيل + عند pause/ended + visibilitychange + pagehide keepalive → OnlineLearning/Ping (OLC-S7.1).
 * - P7 الإتمام وفتح التالي من رد الخادم فقط (completed / nextItemId / lectureCompleted / nextLectureUnlocked).
 * - OLC-S7.2: الاستئناف من MaxPositionSeconds (data-resume من الخادم)، و«insufficient» ⇒ رسالة «أعد المشاهدة من البداية» + الرجوع للبداية.
 * - P11 المكتمل = مراجعة: تنقّل حر بلا نبضات.
 * - لا مزوّد «Other» ولا manual-done في هذه الميزة (YouTube/Vimeo فقط).
 */
(function () {
    'use strict';

    var root = document.getElementById('olcWatchRoot');
    if (!root || root.dataset.olcBound === '1') return;   // لا ربط مزدوج
    root.dataset.olcBound = '1';

    var LECTURE_REVIEW = root.dataset.review === '1';
    var MODE = root.dataset.mode === 'full' ? 'full' : 'items';
    var contentId = parseInt(root.dataset.contentId, 10);
    var pingUrl = root.dataset.pingUrl || '';
    var nextUrl = root.dataset.nextUrl || '';
    var PING_MS = 15000;
    var SKIP_TOLERANCE = 2;                     // ثوانٍ: أكبر قفزة تُعتبر تشغيلًا طبيعيًا
    var RESUME_BACKOFF = 5, RESUME_MIN = 10;

    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
    var stateEl = document.getElementById('olcState');
    var titleEl = document.getElementById('olcNowTitle');
    var wrapEl = document.getElementById('olcPlayerWrap');
    var listEl = document.getElementById('olcItemList');
    var chapterEl = document.getElementById('olcChapterList');
    var nextBox = document.getElementById('olcNextBox');
    var shieldEl = document.getElementById('olcPlayerShield');
    var btnPlay = document.getElementById('olcBtnPlay');
    var btnMute = document.getElementById('olcBtnMute');
    var btnFull = document.getElementById('olcBtnFull');
    var seekEl = document.getElementById('olcSeek');
    var timeEl = document.getElementById('olcTime');

    var current = null;      // { el, itemId, provider, player, playing, timer, maxPos, dur, review, ... }
    var inFlight = false;
    var queued = null;
    var scriptPromises = {};

    function setState(html, kind) {
        if (!stateEl) return;
        stateEl.className = 'olc-player-state' + (kind ? ' text-' + kind : '');
        stateEl.innerHTML = html || '';
    }
    function spinner(text) { return '<span class="olc-spinner"></span>' + text; }
    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : s; return d.innerHTML; }
    function setIcon(btn, cls) { var i = btn && btn.querySelector('i'); if (i) i.className = 'fas ' + cls; }
    function fmt(s) {
        s = Math.max(0, Math.floor(s || 0));
        var h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), r = s % 60;
        return (h ? h + ':' + (m < 10 ? '0' : '') : '') + m + ':' + (r < 10 ? '0' : '') + r;
    }
    function tracking() { return !!pingUrl && !!current && !current.review; }

    // ───────────── تحميل المكتبات ─────────────

    function loadYouTube() {
        if (window.YT && window.YT.Player) return Promise.resolve();
        if (scriptPromises.yt) return scriptPromises.yt;
        scriptPromises.yt = new Promise(function (resolve, reject) {
            var prev = window.onYouTubeIframeAPIReady;
            window.onYouTubeIframeAPIReady = function () { if (prev) { try { prev(); } catch (e) { } } resolve(); };
            var s = document.createElement('script');
            s.src = 'https://www.youtube.com/iframe_api'; s.async = true;
            s.onerror = function () { delete scriptPromises.yt; reject(new Error('load-failed')); };
            document.head.appendChild(s);
        });
        return scriptPromises.yt;
    }
    function loadVimeo() {
        if (window.Vimeo && window.Vimeo.Player) return Promise.resolve();
        if (scriptPromises.vimeo) return scriptPromises.vimeo;
        scriptPromises.vimeo = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = 'https://player.vimeo.com/api/player.js'; s.async = true;
            s.onload = function () { resolve(); };
            s.onerror = function () { delete scriptPromises.vimeo; reject(new Error('load-failed')); };
            document.head.appendChild(s);
        });
        return scriptPromises.vimeo;
    }

    // ───────────── النبضة (P7/P10) — عقد C.5 ─────────────

    function pingBody(c, state, position, duration) {
        var body = { lectureContentId: contentId, state: state, position: position, duration: duration };
        if (c.itemId) body.itemId = c.itemId;
        return body;
    }

    function post(body, attempt) {
        attempt = attempt || 0;
        return fetch(pingUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenInput ? tokenInput.value : '' },
            body: JSON.stringify(body)
        }).then(function (res) {
            return res.json().catch(function () { return {}; }).then(function (data) { return { status: res.status, data: data }; });
        }).catch(function () {
            if (attempt < 3) {   // تراجع أسّي
                setState(spinner('انقطع الاتصال، تتم إعادة المحاولة...'), 'warning');
                return new Promise(function (r) { setTimeout(r, 1500 * Math.pow(2, attempt)); })
                    .then(function () { return post(body, attempt + 1); });
            }
            return { status: 0, data: {} };
        });
    }

    function ping(state) {
        if (!tracking()) return;
        if (inFlight) {
            if (state !== 'playing' && queued !== 'ended') queued = state;
            return;
        }
        var c = current;
        inFlight = true;
        Promise.resolve(c.readTimes ? c.readTimes() : [0, 0]).then(function (t) {
            return post(pingBody(c, state, t[0] || 0, t[1] || 0));
        }).then(function (r) {
            inFlight = false;
            if (current !== c) return;
            handleResponse(r, state);
            if (queued) { var q = queued; queued = null; ping(q); }
        }).catch(function (e) {
            inFlight = false;
            if (window.console && console.warn) console.warn('[OLC] ping error', e);
        });
    }

    function handleResponse(r, sentState) {
        var d = r.data || {};
        if (r.status === 200 && (d.reason === 'locked' || d.reason === 'notfound')) {
            stopTimer();
            setState(esc(d.message || (d.reason === 'locked' ? 'أكمل المحاضرة السابقة أولًا.' : 'هذه المحاضرة غير متاحة حاليًا.')), 'danger');
            return;
        }
        if (r.status === 200 && d.ok) {
            if (current) current.failCount = 0;
            if (sentState === 'ended' && current) current.endedAnswered = true;
            if (d.completed) { onCompleted(d); return; }
            if (d.reason === 'insufficient') {
                setState(esc(d.message || ('لم تكتمل المشاهدة المطلوبة (' + d.watchedSeconds + ' من ' + d.requiredSeconds + ' ثانية). أعد المشاهدة من البداية.')), 'warning');
                if (current && current.restart) current.restart();
            } else if (current && typeof d.requiredSeconds === 'number') {
                current.requiredSeconds = d.requiredSeconds;
            }
            return;
        }
        // عطل مؤقت: لا نوقف المؤقّت ونعيد المحاولة تلقائيًا
        if (r.status === 0 || r.status >= 500 || r.status === 200) {
            if (current) {
                current.failCount = (current.failCount || 0) + 1;
                if (current.failCount < 4) {
                    setState(spinner('انقطاع مؤقت في الاتصال، تتم إعادة المحاولة تلقائيًا...'), 'warning');
                    if (current.playing && !current.timer) startTimer();
                    return;
                }
            }
            stopTimer();
            setState('تعذر الاتصال بالخادم (رمز ' + r.status + '). <a href="' + esc(location.href) + '">حدّث الصفحة</a> — مشاهدتك المسجّلة محفوظة.', 'danger');
            return;
        }
        stopTimer();
        if (r.status === 401) setState('انتهت جلستك. <a href="' + esc(location.href) + '">سجّل الدخول من جديد</a>.', 'danger');
        else if (r.status === 409) setState(esc(d.message || 'لا يمكن تسجيل المشاهدة الآن. أعد تحميل الصفحة.'), 'danger');
        else if (r.status === 429) {
            setState('طلبات كثيرة خلال وقت قصير، سيُستأنف التسجيل تلقائيًا.', 'warning');
            if (current && current.playing) startTimer();
        }
        else if (r.status === 404 || r.status === 403) setState('هذه المحاضرة غير متاحة حاليًا.', 'danger');
        else setState('تعذر الاتصال بالخادم. تحقق من الإنترنت ثم أعد تحميل الصفحة.', 'danger');
    }

    // الإتمام من الخادم فقط (P7)
    function onCompleted(d) {
        var c = current;
        stopTimer();
        if (MODE === 'items' && c && c.el) {
            c.el.dataset.completed = '1';
            c.el.classList.add('is-done');
            var icon = c.el.querySelector('.olc-item-icon');
            if (icon) icon.className = 'olc-item-icon fas fa-circle-check';
        }
        if (d.lectureCompleted) {
            destroyCurrent();
            showNext(d.nextLectureUnlocked);
            setState('✓ أتممت هذه المحاضرة.' + (d.nextLectureUnlocked && nextUrl ? ' يمكنك الانتقال للمحاضرة التالية.' : ''), 'success');
            return;
        }
        if (MODE === 'full') {
            setState('✓ تم تسجيل المشاهدة.', 'success');
            return;
        }
        destroyCurrent();
        setState(spinner('✓ اكتمل الفيديو. جارٍ تحديث القائمة...'), 'success');
        syncFromServer().then(function (ok) {
            var next = ok && listEl && (d.nextItemId
                ? listEl.querySelector('li[data-item-id="' + d.nextItemId + '"]')
                : listEl.querySelector('li[data-unlocked="1"][data-completed="0"]'));
            if (next && next.dataset.externalId) {
                setState('✓ اكتمل الفيديو. سيبدأ التالي...', 'success');
                setTimeout(function () { try { select(next, true); } catch (e) { reloadToContinue(); } }, 800);
                return;
            }
            reloadToContinue();
        });
    }

    function showNext(unlocked) {
        if (!nextBox) return;
        if (!nextUrl || unlocked) nextBox.hidden = false;
    }

    // يجلب الصفحة نفسها ويستبدل القائمة فقط (الخادم هو المرجع — والمصدر يأتي منه وفق P6)
    function syncFromServer() {
        return fetch(location.href, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'fetch' } })
            .then(function (res) { if (!res.ok) throw new Error('http-' + res.status); return res.text(); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var fresh = doc.getElementById('olcItemList');
                if (!fresh || !listEl) return false;
                listEl.innerHTML = fresh.innerHTML;
                var cnt = doc.getElementById('olcItemsCount'), cntEl = document.getElementById('olcItemsCount');
                if (cnt && cntEl) cntEl.textContent = cnt.textContent;
                return true;
            })
            .catch(function (e) { if (window.console && console.warn) console.warn('[OLC] sync failed', e); return false; });
    }

    function reloadToContinue() {
        setState('✓ تم الحفظ. جارٍ تحديث الصفحة... <a href="' + esc(location.href) + '">اضغط هنا إن لم تتحدّث</a>', 'success');
        setTimeout(function () { location.reload(); }, 1500);
    }

    // ───────────── المؤقّت ─────────────

    function startTimer() {
        stopTimer();
        if (!tracking()) return;
        current.timer = setInterval(function () { ping('playing'); }, PING_MS);
    }
    function stopTimer() {
        if (current && current.timer) { clearInterval(current.timer); current.timer = null; }
    }

    function onPlayerState(state) {   // playing | paused | ended
        if (!current) return;
        current.playing = state === 'playing';
        setIcon(btnPlay, current.playing ? 'fa-pause' : 'fa-play');
        ping(state);
        if (state === 'playing') startTimer(); else stopTimer();
        if (state === 'ended' && tracking()) watchEnded(current);
    }

    // إن لم يصل ردّ الخادم على «ended» أعد الإرسال ثم اعرض رابط تحديث
    function watchEnded(c) {
        var tries = 0;
        c.endedAnswered = false;
        (function check() {
            setTimeout(function () {
                if (current !== c || c.endedAnswered) return;
                if (++tries > 3) {
                    setState('تعذّر تأكيد إنهاء الفيديو. <a href="' + esc(location.href) + '">حدّث الصفحة</a> (مشاهدتك محفوظة).', 'danger');
                    return;
                }
                ping('ended');
                check();
            }, 5000);
        })();
    }

    // ───────────── المشغّلات ─────────────

    function destroyCurrent() {
        stopTimer();
        if (current && current.poll) { clearInterval(current.poll); current.poll = null; }
        if (current && current.player) { try { current.player.destroy(); } catch (e) { } }
        var host = document.getElementById('olcPlayerHost');
        if (host) host.innerHTML = '';
        current = null;
        queued = null;
    }

    function fail(msg) { setState(esc(msg), 'danger'); }

    // نقطة الاستئناف: أبعد موضع وثّقه الخادم (data-resume) مع رجوع بسيط؛ لا استئناف في المراجعة
    function resumePointFor(el, review) {
        if (review) return 0;
        var watched = parseInt(el.dataset.resume || '0', 10);
        var dur = parseInt(el.dataset.duration || '0', 10);
        if (!(watched > RESUME_MIN)) return 0;
        var t = watched - RESUME_BACKOFF;
        if (dur > 0) t = Math.min(t, dur - 5);
        return t > 0 ? Math.floor(t) : 0;
    }
    function applyResume(c) {
        if (!c.resumeAt) return;
        try { c.seek(c.resumeAt); } catch (e) { }
        renderSeek(c.resumeAt, c.dur, c.maxPos);
        setState('تم الاستئناف من الدقيقة ' + fmt(c.resumeAt) + ' حيث توقفت سابقًا.', 'success');
    }

    // el = عنصر القائمة (items) أو الجذر (full). autoplay بعد إتمام سابق (تفاعل الطالب يسمح بالتشغيل)
    function mount(el, itemId, autoplay) {
        destroyCurrent();
        var review = LECTURE_REVIEW || el.dataset.completed === '1';   // P11
        var serverMax = parseInt(el.dataset.resume || '0', 10) || 0;
        current = {
            el: el, itemId: itemId, provider: el.dataset.provider,
            player: null, playing: false, timer: null, review: review,
            // P4: أبعد نقطة يحق التنقل إليها = ما وثّقه الخادم (المراجعة: تنقّل حر)
            maxPos: review ? Number.MAX_SAFE_INTEGER : serverMax, dur: parseInt(el.dataset.duration || '0', 10) || 0, dragging: false,
            requiredSeconds: parseInt(el.dataset.required || '0', 10),
            autoplay: !!autoplay, resumeAt: 0
        };
        current.resumeAt = resumePointFor(el, review);
        renderSeek(0, current.dur, current.maxPos);
        setIcon(btnPlay, 'fa-play');
        setIcon(btnMute, 'fa-volume-up');
        if (seekEl) seekEl.setAttribute('aria-label', review ? 'موضع الفيديو' : 'موضع الفيديو (يمكنك الرجوع فقط داخل ما شاهدته)');
        setState(spinner('جارٍ تحميل المشغّل...'));

        if (current.provider === 'YouTube') mountYouTube(el);
        else if (current.provider === 'Vimeo') mountVimeo(el);
        else fail('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.');
    }

    function mountYouTube(el) {
        var c = current;
        var vid = el.dataset.externalId;
        loadYouTube().then(function () {
            if (current !== c) return;
            var host = document.getElementById('olcPlayerHost');
            var div = document.createElement('div'); host.appendChild(div);
            c.player = new YT.Player(div, {
                videoId: vid,
                host: 'https://www.youtube-nocookie.com',   // R8: نطاق التضمين المعتمد
                playerVars: { rel: 0, modestbranding: 1, playsinline: 1, controls: 0, disablekb: 1, fs: 0, iv_load_policy: 3, origin: location.origin },
                events: {
                    onReady: function () {
                        setState(c.review ? 'وضع المراجعة: تنقّل بحرية (لا يُحتسب).' : 'اضغط ▶ للبدء.');
                        applyResume(c);
                        if (c.autoplay) { try { c.player.playVideo(); } catch (e) { } }
                    },
                    onStateChange: function (e) {
                        if (e.data === 1) onPlayerState('playing');
                        else if (e.data === 2) onPlayerState('paused');
                        else if (e.data === 0) onPlayerState('ended');
                    },
                    onError: function () { fail('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.'); }
                }
            });
            c.readTimes = function () { return [c.player.getCurrentTime(), c.player.getDuration()]; };
            c.restart = function () { c.maxPos = 0; try { c.player.seekTo(0, true); } catch (e) { } };
            c.seek = function (t) { c.player.seekTo(t, true); };
            c.play = function () { c.player.playVideo(); };
            c.poll = setInterval(function () {
                try { tick(c, c.player.getCurrentTime(), c.player.getDuration()); } catch (e) { }
            }, 500);
            c.togglePlay = function () { if (c.playing) c.player.pauseVideo(); else c.player.playVideo(); };
            c.toggleMute = function () { if (c.player.isMuted()) c.player.unMute(); else c.player.mute(); return c.player.isMuted(); };
        }).catch(function () { fail('تعذر تحميل مشغّل YouTube (قد يكون محظورًا في شبكتك). جرّب شبكة أخرى أو تواصل مع الإدارة.'); });
    }

    function mountVimeo(el) {
        var c = current;
        loadVimeo().then(function () {
            if (current !== c) return;
            var host = document.getElementById('olcPlayerHost');
            c.player = new Vimeo.Player(host, { url: el.dataset.embedUrl, responsive: true, controls: false, title: false, byline: false, portrait: false, keyboard: false });
            c.player.ready().then(function () {
                setState(c.review ? 'وضع المراجعة: تنقّل بحرية (لا يُحتسب).' : 'اضغط ▶ للبدء.');
                applyResume(c);
                if (c.autoplay) { try { c.player.play().catch(function () { }); } catch (e) { } }
            }).catch(function () { fail('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.'); });
            c.player.on('play', function () { onPlayerState('playing'); });
            c.player.on('pause', function () { onPlayerState('paused'); });
            c.player.on('ended', function () { onPlayerState('ended'); });
            c.player.on('error', function () { fail('تعذر تشغيل هذا الفيديو. تواصل مع الإدارة.'); });
            c.readTimes = function () {
                return Promise.all([c.player.getCurrentTime(), c.player.getDuration()]).catch(function () { return [0, 0]; });
            };
            c.restart = function () { c.maxPos = 0; try { c.player.setCurrentTime(0); } catch (e) { } };
            c.seek = function (t) { c.player.setCurrentTime(t); };
            c.play = function () { c.player.play().catch(function () { }); };
            c.player.on('timeupdate', function (d) { tick(c, d.seconds, d.duration); });
            c.togglePlay = function () { if (c.playing) c.player.pause(); else c.player.play(); };
            c.toggleMute = function () { c.vimeoMuted = !c.vimeoMuted; c.player.setMuted(c.vimeoMuted); return c.vimeoMuted; };
        }).catch(function () { fail('تعذر تحميل مشغّل Vimeo (قد يكون محظورًا في شبكتك). جرّب شبكة أخرى أو تواصل مع الإدارة.'); });
    }

    // ───────────── P4: لا تقديم — الرجوع داخل ما شوهد فقط ─────────────

    function renderSeek(t, d, maxPos) {
        if (!seekEl) return;
        seekEl.value = d > 0 ? Math.min(1000, (t / d) * 1000) : 0;
        var pct = d > 0 ? Math.min(100, (maxPos / d) * 100) : 0;
        seekEl.style.background = 'linear-gradient(to right, #1a7f6e ' + pct + '%, rgba(255,255,255,.3) ' + pct + '%)';
        if (timeEl) timeEl.textContent = fmt(t) + ' / ' + fmt(d);
    }
    function tick(c, t, d) {
        if (current !== c || typeof t !== 'number' || !isFinite(t)) return;
        if (d > 0) c.dur = d;
        if (t > c.maxPos + SKIP_TOLERANCE) {          // محاولة تخطٍّ: أعده لأبعد نقطة شاهدها
            try { c.seek(c.maxPos); } catch (e) { }
            return;
        }
        if (t > c.maxPos) c.maxPos = t;
        c.lastT = t;
        if (!c.dragging) renderSeek(t, c.dur, c.maxPos);
        if (MODE === 'full') highlightChapter(t);
    }
    if (seekEl) {
        seekEl.addEventListener('input', function () {
            if (!current || !current.dur) return;
            current.dragging = true;
            var target = Math.min((seekEl.value / 1000) * current.dur, current.maxPos);
            if (timeEl) timeEl.textContent = fmt(target) + ' / ' + fmt(current.dur);
        });
        seekEl.addEventListener('change', function () {
            if (!current || !current.dur || !current.seek) return;
            var target = Math.min((seekEl.value / 1000) * current.dur, current.maxPos);
            current.dragging = false;
            try { current.seek(target); } catch (e) { }
            renderSeek(target, current.dur, current.maxPos);
        });
    }

    // ───────────── FullVideo: الفصول — القفز فقط داخل ما شوهد ─────────────

    var lastChapter = null;
    function highlightChapter(t) {
        if (!chapterEl) return;
        var hit = null;
        [].forEach.call(chapterEl.querySelectorAll('li[data-start]'), function (li) {
            var s = parseFloat(li.dataset.start || '0'), e = parseFloat(li.dataset.end || '0');
            if (t >= s && (!(e > 0) || t < e)) hit = li;
        });
        if (hit === lastChapter) return;
        if (lastChapter) lastChapter.classList.remove('is-active');
        if (hit) hit.classList.add('is-active');
        lastChapter = hit;
    }

    if (chapterEl) {
        chapterEl.addEventListener('click', function (e) {
            var b = e.target.closest('.olc-go-chapter');
            if (!b || !current || !current.seek) return;
            var li = b.closest('li[data-start]');
            var start = parseFloat(li.dataset.start || '0');
            if (start > current.maxPos + SKIP_TOLERANCE) {
                setState('لم تصل لهذا الجزء بعد. تابع المشاهدة بالترتيب.', 'warning');
                return;
            }
            try { current.seek(start); if (!current.playing && current.play) current.play(); } catch (err) { }
            renderSeek(start, current.dur, current.maxPos);
            setState('');
        });
    }

    // ───────────── IndicatorVideos: قائمة تشغيل متسلسلة (R14) ─────────────

    function select(li, autoplay) {
        if (li.dataset.unlocked !== '1' || !li.dataset.externalId) return;   // المقفل لا يُشغَّل (والخادم يرفض نبضاته)
        [].forEach.call(listEl.querySelectorAll('.olc-item'), function (x) { x.classList.remove('is-active'); });
        li.classList.add('is-active');
        if (titleEl) titleEl.textContent = li.dataset.title || '';
        mount(li, parseInt(li.dataset.itemId, 10), autoplay);
        if (wrapEl) wrapEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    if (listEl) {
        listEl.addEventListener('click', function (e) {
            var b = e.target.closest('.olc-select-item');
            if (!b) return;
            var li = b.closest('.olc-item');
            if (li) select(li);
        });
    }

    // ───────────── P2/P8: الطبقة والرادع ─────────────

    function togglePlay() { if (current && current.togglePlay) { try { current.togglePlay(); } catch (e) { } } }

    if (wrapEl) {
        wrapEl.addEventListener('contextmenu', function (e) { e.preventDefault(); });
        wrapEl.addEventListener('dragstart', function (e) { e.preventDefault(); });
    }
    document.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    document.addEventListener('dragstart', function (e) { if (e.target && e.target.closest && e.target.closest('#olcWatchRoot')) e.preventDefault(); });
    document.addEventListener('keydown', function (e) {
        var k = (e.key || '').toLowerCase();
        var ctrl = e.ctrlKey || e.metaKey;
        var devtools = k === 'f12' || (ctrl && e.shiftKey && (k === 'i' || k === 'j' || k === 'c')) || (e.metaKey && e.altKey && (k === 'i' || k === 'j' || k === 'c'));
        if (devtools || (ctrl && (k === 'u' || k === 's'))) { e.preventDefault(); e.stopPropagation(); }
    }, true);

    if (shieldEl) shieldEl.addEventListener('click', togglePlay);
    if (btnPlay) btnPlay.addEventListener('click', togglePlay);
    if (btnMute) {
        btnMute.addEventListener('click', function () {
            if (!current || !current.toggleMute) return;
            try { setIcon(btnMute, current.toggleMute() ? 'fa-volume-mute' : 'fa-volume-up'); } catch (e) { }
        });
    }

    // ───────────── P9: ملء الشاشة على الحاوية (+ بديل CSS) ─────────────

    function fsElement() { return document.fullscreenElement || document.webkitFullscreenElement || null; }
    function requestFs(el) {
        var fn = el.requestFullscreen || el.webkitRequestFullscreen || el.msRequestFullscreen;
        if (!fn) return Promise.reject(new Error('no-fs-api'));
        try { var r = fn.call(el); return (r && r.then) ? r : Promise.resolve(); }
        catch (e) { return Promise.reject(e); }
    }
    function exitFs() {
        var fn = document.exitFullscreen || document.webkitExitFullscreen;
        if (fn) { try { fn.call(document); } catch (e) { } }
    }
    function lockOrientation(on) {
        try {
            if (on && screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(function () { });
            if (!on && screen.orientation && screen.orientation.unlock) screen.orientation.unlock();
        } catch (e) { }
    }
    function setPseudoFs(on) {
        wrapEl.classList.toggle('olc-pseudo-fs', on);
        document.documentElement.classList.toggle('olc-no-scroll', on);
        setIcon(btnFull, on ? 'fa-compress' : 'fa-expand');
        lockOrientation(on);
    }
    if (btnFull && wrapEl) {
        btnFull.addEventListener('click', function () {
            if (wrapEl.classList.contains('olc-pseudo-fs')) { setPseudoFs(false); return; }
            if (fsElement()) { exitFs(); return; }
            requestFs(wrapEl).catch(function () { setPseudoFs(true); });
        });
        ['fullscreenchange', 'webkitfullscreenchange'].forEach(function (ev) {
            document.addEventListener(ev, function () {
                var on = !!fsElement();
                setIcon(btnFull, on ? 'fa-compress' : 'fa-expand');
                lockOrientation(on);
            });
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && wrapEl.classList.contains('olc-pseudo-fs')) setPseudoFs(false);
        });
    }

    // ───────────── P10: دورة الصفحة ─────────────

    document.addEventListener('visibilitychange', function () {
        if (!current) return;
        ping(current.playing ? 'playing' : 'paused');
    });
    window.addEventListener('pagehide', function () {
        if (!tracking() || !current.playing) return;
        try {
            fetch(pingUrl, {
                method: 'POST', keepalive: true, credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenInput ? tokenInput.value : '' },
                body: JSON.stringify(pingBody(current, 'paused', current.lastT || 0, current.dur || 0))
            });
        } catch (e) { }
    });

    // ───────────── البدء ─────────────

    if (MODE === 'full') {
        if (root.dataset.externalId) mount(root, null, false);
        return;
    }
    if (LECTURE_REVIEW) {
        setState('وضع المراجعة: اضغط «مراجعة» بجوار أي فيديو للمشاهدة (لا يُحتسب).');
        return;
    }
    var first = listEl && listEl.querySelector('li[data-unlocked="1"][data-completed="0"]');
    if (first && first.dataset.externalId) setState('اضغط «مشاهدة» بجوار الفيديو للبدء.');
    else if (first) setState('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.', 'danger');
})();
