/* RTK-S4.4 — مشغّل فيديو الخطة العلاجية (YouTube IFrame API / Vimeo Player SDK)
 * - الإتمام وفتح التالي من رد الخادم فقط (D4) — لا يُفتح فيديو من JS وحده.
 * - نبضة كل 15 ثانية أثناء التشغيل + عند pause/ended + عند تغيّر ظهور الصفحة.
 */
(function () {
    'use strict';

    var root = document.getElementById('rtkRoot');
    if (!root || root.dataset.canWatch !== '1') return;
    if (root.dataset.rtkBound === '1') return;   // لا ربط مزدوج
    root.dataset.rtkBound = '1';

    var PING_MS = 15000;
    var enrollmentId = parseInt(root.dataset.enrollmentId, 10);
    var pingUrl = root.dataset.pingUrl;
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
    var listEl = document.getElementById('rtkVideoList');
    var stateEl = document.getElementById('rtkState');
    var titleEl = document.getElementById('rtkNowTitle');
    var wrapEl = document.getElementById('rtkPlayerWrap');
    var otherBox = document.getElementById('rtkOtherBox');
    var otherLink = document.getElementById('rtkOtherLink');
    var manualBtn = document.getElementById('rtkManualDone');

    var current = null;      // { li, id, provider, player, lastState, timer, ... }
    var inFlight = false;
    var queued = null;       // آخر حالة مهمة ننتظر إرسالها
    var scriptPromises = {};

    function setState(html, kind) {
        if (!stateEl) return;
        stateEl.className = 'rtk-player-state' + (kind ? ' text-' + kind : '');
        stateEl.innerHTML = html || '';
    }
    function spinner(text) { return '<span class="rtk-spinner"></span>' + text; }
    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : s; return d.innerHTML; }

    function loadScript(src, key) {
        if (scriptPromises[key]) return scriptPromises[key];
        scriptPromises[key] = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = src; s.async = true;
            s.onload = function () { resolve(); };
            s.onerror = function () { delete scriptPromises[key]; reject(new Error('load-failed')); };
            document.head.appendChild(s);
        });
        return scriptPromises[key];
    }

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
        return loadScript('https://player.vimeo.com/api/player.js', 'vimeo');
    }

    // ───────────── الإرسال ─────────────

    function post(body, attempt) {
        attempt = attempt || 0;
        return fetch(pingUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: JSON.stringify(body)
        }).then(function (res) {
            return res.json().catch(function () { return {}; }).then(function (data) {
                return { status: res.status, data: data };
            });
        }).catch(function () {
            if (attempt < 3) {
                setState(spinner('انقطع الاتصال، تتم إعادة المحاولة...'), 'warning');
                return new Promise(function (r) { setTimeout(r, 1500 * Math.pow(2, attempt)); })
                    .then(function () { return post(body, attempt + 1); });
            }
            return { status: 0, data: {} };
        });
    }

    function ping(state) {
        if (!current) return;
        if (inFlight) {
            // نبضة دورية أثناء الانتظار تُهمل؛ الحالات المهمة تُؤجَّل
            if (state !== 'playing') queued = state;
            return;
        }
        var c = current;
        inFlight = true;
        var pos = 0, dur = 0;
        Promise.resolve(c.readTimes ? c.readTimes() : [0, 0]).then(function (t) {
            pos = t[0] || 0; dur = t[1] || 0;
            return post({
                enrollmentId: enrollmentId,
                videoProgressId: c.id,
                state: state,
                position: pos,
                duration: dur
            });
        }).then(function (r) {
            inFlight = false;
            if (current !== c) return;
            handleResponse(r, state);
            if (queued) { var q = queued; queued = null; ping(q); }
        });
    }

    function handleResponse(r, sentState) {
        if (r.status === 200 && r.data && r.data.ok) {
            var d = r.data;
            if (d.completed) { onCompleted(d); return; }
            if (d.reason === 'insufficient') {
                setState('لم تكتمل المشاهدة المطلوبة (' + d.watchedSeconds + ' من ' + d.requiredSeconds + ' ثانية). أعد المشاهدة من البداية.', 'warning');
                if (current && current.restart) current.restart();
            } else if (current && current.requiredSeconds !== d.requiredSeconds) {
                current.requiredSeconds = d.requiredSeconds;
            }
            if (current && current.provider === 'Other' && manualBtn) {
                manualBtn.disabled = !(d.watchedSeconds >= d.requiredSeconds);
            }
            return;
        }
        stopTimer();
        if (r.status === 403 && r.data && r.data.needsCode) {
            setState('انتهت صلاحية الرقم المرجعي. <a href="' + esc(root.dataset.openUrl) + '">أدخل الرقم الجديد</a>', 'danger');
        } else if (r.status === 409) {
            setState(esc(r.data && r.data.message || 'لا يمكن تسجيل المشاهدة الآن. أعد تحميل الصفحة.'), 'danger');
        } else if (r.status === 429) {
            setState('طلبات كثيرة خلال وقت قصير، سيُستأنف التسجيل تلقائيًا.', 'warning');
            if (current && current.playing) startTimer();
        } else if (r.status === 404 || r.status === 403) {
            setState('هذه الخطة غير متاحة حاليًا. تواصل مع الإدارة.', 'danger');
        } else {
            setState('تعذر الاتصال بالخادم. تحقق من الإنترنت ثم أعد تحميل الصفحة.', 'danger');
        }
    }

    // الإتمام: يأتي من الخادم فقط
    function onCompleted(d) {
        var li = current.li;
        li.dataset.completed = '1';
        li.classList.add('is-done');
        var icon = li.querySelector('.rtk-video-icon');
        if (icon) icon.className = 'rtk-video-icon fas fa-circle-check';
        var btn = li.querySelector('.rtk-select-video');
        if (btn) btn.remove();
        stopTimer();
        destroyCurrent();

        if (d.allDone) {
            setState('أحسنت! أنهيت جميع فيديوهات المحور. جارٍ تحديث الصفحة...', 'success');
            setTimeout(function () { location.reload(); }, 1800);
            return;
        }
        if (d.nextVideo) {
            var next = listEl.querySelector('li[data-vp-id="' + d.nextVideo.videoProgressId + '"]');
            if (next) {
                unlock(next);
                setState('✓ اكتمل الفيديو. سيبدأ التالي...', 'success');
                setTimeout(function () { select(next); }, 1200);
                return;
            }
        }
        setState('✓ اكتمل الفيديو.', 'success');
    }

    function unlock(li) {
        li.dataset.unlocked = '1';
        li.classList.remove('is-locked');
        var icon = li.querySelector('.rtk-video-icon');
        if (icon) icon.className = 'rtk-video-icon fas fa-circle-play';
        if (!li.querySelector('.rtk-select-video')) {
            var b = document.createElement('button');
            b.type = 'button'; b.className = 'rtk-btn rtk-btn-outline rtk-select-video'; b.textContent = 'مشاهدة';
            li.appendChild(b);
        }
    }

    // ───────────── المؤقّت ─────────────

    function startTimer() {
        stopTimer();
        if (!current) return;
        current.timer = setInterval(function () { ping('playing'); }, PING_MS);
    }
    function stopTimer() {
        if (current && current.timer) { clearInterval(current.timer); current.timer = null; }
    }

    function onPlayerState(state) {   // playing | paused | ended
        if (!current) return;
        current.playing = state === 'playing';
        ping(state);
        if (state === 'playing') startTimer(); else stopTimer();
    }

    // ───────────── المشغّلات ─────────────

    function destroyCurrent() {
        stopTimer();
        if (current && current.player) {
            try { if (current.provider === 'Vimeo') current.player.destroy(); else current.player.destroy(); } catch (e) { }
        }
        var host = document.getElementById('rtkPlayerHost');
        if (host) host.innerHTML = '';
        current = null;
        queued = null;
    }

    function select(li) {
        if (li.dataset.unlocked !== '1' || li.dataset.completed === '1') return;
        destroyCurrent();
        var provider = li.dataset.provider;
        current = {
            li: li, id: parseInt(li.dataset.vpId, 10), provider: provider,
            player: null, playing: false, timer: null,
            requiredSeconds: parseInt(li.dataset.required || '0', 10)
        };
        [].forEach.call(listEl.querySelectorAll('.rtk-video-item'), function (x) { x.classList.remove('is-active'); });
        li.classList.add('is-active');
        if (titleEl) titleEl.textContent = li.dataset.title || '';

        if (wrapEl) wrapEl.hidden = provider === 'Other';
        if (otherBox) otherBox.hidden = provider !== 'Other';
        setState(spinner('جارٍ تحميل المشغّل...'));

        if (provider === 'YouTube') mountYouTube(li);
        else if (provider === 'Vimeo') mountVimeo(li);
        else mountOther(li);

        wrapEl && wrapEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    function fail(msg) { setState(esc(msg), 'danger'); }

    function mountYouTube(li) {
        var c = current;
        var vid = li.dataset.externalId;
        loadYouTube().then(function () {
            if (current !== c) return;
            var host = document.getElementById('rtkPlayerHost');
            var el = document.createElement('div'); host.appendChild(el);
            c.player = new YT.Player(el, {
                videoId: vid,
                playerVars: { rel: 0, modestbranding: 1, playsinline: 1 },
                events: {
                    onReady: function () { setState(''); },
                    onStateChange: function (e) {
                        if (e.data === 1) onPlayerState('playing');
                        else if (e.data === 2) onPlayerState('paused');
                        else if (e.data === 0) onPlayerState('ended');
                    },
                    onError: function () { fail('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.'); }
                }
            });
            c.readTimes = function () { return [c.player.getCurrentTime(), c.player.getDuration()]; };
            c.restart = function () { try { c.player.seekTo(0, true); } catch (e) { } };
        }).catch(function () { fail('تعذر تحميل مشغّل YouTube (قد يكون محظورًا في شبكتك). جرّب شبكة أخرى أو تواصل مع الإدارة.'); });
    }

    function mountVimeo(li) {
        var c = current;
        loadVimeo().then(function () {
            if (current !== c) return;
            var host = document.getElementById('rtkPlayerHost');
            c.player = new Vimeo.Player(host, { url: li.dataset.embedUrl, responsive: true });
            c.player.ready().then(function () { setState(''); }).catch(function () { fail('هذا الفيديو غير متاح حاليًا. تواصل مع الإدارة.'); });
            c.player.on('play', function () { onPlayerState('playing'); });
            c.player.on('pause', function () { onPlayerState('paused'); });
            c.player.on('ended', function () { onPlayerState('ended'); });
            c.player.on('error', function () { fail('تعذر تشغيل هذا الفيديو. تواصل مع الإدارة.'); });
            c.readTimes = function () {
                return Promise.all([c.player.getCurrentTime(), c.player.getDuration()]).catch(function () { return [0, 0]; });
            };
            c.restart = function () { try { c.player.setCurrentTime(0); } catch (e) { } };
        }).catch(function () { fail('تعذر تحميل مشغّل Vimeo (قد يكون محظورًا في شبكتك). جرّب شبكة أخرى أو تواصل مع الإدارة.'); });
    }

    // منصة أخرى (D5): الواجهة ترسل playing وهي ظاهرة، والإتمام بزر «أنهيت المشاهدة» يتحقق منه الخادم
    function mountOther(li) {
        var c = current;
        if (otherLink) otherLink.href = li.dataset.url;
        if (manualBtn) manualBtn.disabled = true;
        c.readTimes = function () { return [0, parseInt(li.dataset.duration || '0', 10)]; };
        c.playing = document.visibilityState === 'visible';
        setState('افتح الفيديو وشاهده كاملًا؛ يُفعَّل زر «أنهيت المشاهدة» بعد انقضاء الزمن المطلوب.');
        ping('playing');
        startTimer();
    }

    // ───────────── الأحداث ─────────────

    listEl.addEventListener('click', function (e) {
        var b = e.target.closest('.rtk-select-video');
        if (!b) return;
        var li = b.closest('.rtk-video-item');
        if (li) select(li);
    });

    if (manualBtn) {
        manualBtn.addEventListener('click', function () {
            if (!current || current.provider !== 'Other') return;
            manualBtn.disabled = true;
            setState(spinner('جارٍ التحقق من المشاهدة...'));
            ping('manual-done');
        });
    }

    document.addEventListener('visibilitychange', function () {
        if (!current) return;
        if (current.provider === 'Other') {
            if (document.visibilityState === 'hidden') { stopTimer(); ping('paused'); }
            else { ping('playing'); startTimer(); }
            return;
        }
        // مشغّلات YouTube/Vimeo: أرسل حالة الآن؛ النبض الدوري يستمر فقط إن كان التشغيل جاريًا
        ping(current.playing ? 'playing' : 'paused');
    });

    window.addEventListener('pagehide', function () {
        if (!current || !current.playing) return;
        try {
            var body = JSON.stringify({ enrollmentId: enrollmentId, videoProgressId: current.id, state: 'paused', position: 0, duration: 0 });
            fetch(pingUrl, {
                method: 'POST', keepalive: true, credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': tokenInput ? tokenInput.value : '' },
                body: body
            });
        } catch (e) { }
    });

    // أول فيديو متاح وغير مكتمل
    var first = listEl.querySelector('li[data-unlocked="1"][data-completed="0"]');
    if (first) {
        if (titleEl) titleEl.textContent = first.dataset.title || '';
        setState('اضغط «مشاهدة» للبدء.');
    }
})();
