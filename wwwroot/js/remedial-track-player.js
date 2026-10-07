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
            // نبضة دورية أثناء الانتظار تُهمل؛ الحالات المهمة تُؤجَّل، و«ended» لا يستبدله أي حدث لاحق
            if (state !== 'playing' && queued !== 'ended') queued = state;
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
        }).catch(function (e) {
            inFlight = false;   // لا يبقى الإرسال عالقًا إن فشلت قراءة المشغّل أو معالجة الردّ
            if (window.console && console.warn) console.warn('[RTK] ping error', e);
        });
    }

    function handleResponse(r, sentState) {
        if (r.status === 200 && r.data && r.data.ok) {
            var d = r.data;
            if (current) current.failCount = 0;
            if (sentState === 'ended' && current) current.endedAnswered = true;
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
        // عطل مؤقت (انقطاع شبكة / إعادة تشغيل الخادم / 5xx / ردّ غير JSON): لا نوقف المؤقّت ونعيد المحاولة تلقائيًا
        if (r.status === 0 || r.status >= 500 || r.status === 200) {
            if (current) {
                current.failCount = (current.failCount || 0) + 1;
                if (window.console && console.warn) console.warn('[RTK] ping failed, status=' + r.status, r.data);
                if (current.failCount < 4) {
                    setState(spinner('انقطاع مؤقت في الاتصال، تتم إعادة المحاولة تلقائيًا...'), 'warning');
                    if (current.playing && !current.timer) startTimer();
                    return;   // حدث «ended» يعيد إرساله مراقب watchEnded
                }
            }
            stopTimer();
            setState('تعذر الاتصال بالخادم (رمز ' + r.status + '). <a href="' + esc(location.href) + '">حدّث الصفحة</a> — مشاهدتك المسجّلة محفوظة.', 'danger');
            return;
        }
        stopTimer();
        if (r.status === 401) {
            setState('انتهت جلستك. <a href="' + esc(location.href) + '">سجّل الدخول من جديد</a>.', 'danger');
        } else if (r.status === 403 && r.data && r.data.needsCode) {
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

    // الإتمام: يأتي من الخادم فقط. بعده نجلب حالة الصفحة من الخادم (المرجع الوحيد) ونحدّث القائمة وبطاقة الاختبار بلا إعادة تحميل
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
        setState(spinner('✓ اكتمل الفيديو. جارٍ تحديث القائمة...'), 'success');

        syncFromServer().then(function (ok) {
            if (ok && afterSync(d)) return;
            // تعذّر الجلب أو لم تُظهر النسخة المجلوبة فيديو متاحًا: ارجع لما أرسله ردّ النبضة، وإلا فأعد تحميل الصفحة كملاذ أخير
            if (applyNextFromResponse(d)) return;
            reloadToContinue();
        });
    }

    // يفتح الفيديو التالي من ردّ الخادم (nextVideo) ويشغّله؛ يعيد true إن وُجد
    function applyNextFromResponse(d) {
        if (!d || !d.nextVideo) return false;
        var next = listEl.querySelector('li[data-vp-id="' + d.nextVideo.videoProgressId + '"]');
        if (!next) return false;
        next.dataset.provider = d.nextVideo.provider || next.dataset.provider;
        next.dataset.externalId = d.nextVideo.externalId || '';
        next.dataset.embedUrl = d.nextVideo.embedUrl || '';
        next.dataset.url = d.nextVideo.url || '';
        unlock(next);
        setState('✓ اكتمل الفيديو. سيبدأ التالي...', 'success');
        setTimeout(function () { try { select(next, true); } catch (e) { reloadToContinue(); } }, 800);
        return true;
    }

    // يجلب صفحة المحور نفسها ويستبدل قائمة الفيديوهات ومنطقة الاختبار فقط (الخادم هو المرجع)
    function syncFromServer() {
        return fetch(location.href, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'fetch' } })
            .then(function (res) { if (!res.ok) throw new Error('http-' + res.status); return res.text(); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var newList = doc.getElementById('rtkVideoList');
                if (!newList) return false;
                listEl.innerHTML = newList.innerHTML;
                var newExam = doc.getElementById('rtkExamArea');
                var examEl = document.getElementById('rtkExamArea');
                if (newExam && examEl) examEl.innerHTML = newExam.innerHTML;
                return true;
            })
            .catch(function (e) {
                if (window.console && console.warn) console.warn('[RTK] sync failed', e);
                return false;
            });
    }

    // بعد المزامنة: شغّل أول فيديو متاح غير مكتمل، وإن انتهت الفيديوهات أظهر بطاقة الاختبار
    // يعيد true إن عولجت الحالة (تشغيل التالي أو إظهار الاختبار)، وfalse إن لزم الاعتماد على ردّ النبضة/التحميل الكامل
    function afterSync(d) {
        var next = listEl.querySelector('li[data-unlocked="1"][data-completed="0"]');
        if (next) {
            setState('✓ اكتمل الفيديو. سيبدأ التالي...', 'success');
            setTimeout(function () { try { select(next, true); } catch (e) { reloadToContinue(); } }, 800);
            return true;
        }
        // النسخة المجلوبة لا تُظهر تاليًا مع أن ردّ النبضة أعطانا إياه: نثق بردّ الخادم المباشر
        if (applyNextFromResponse(d)) return true;
        if (titleEl) titleEl.textContent = '';
        if (wrapEl) wrapEl.hidden = true;
        var exam = document.getElementById('rtkExamArea');
        if (exam && exam.querySelector('.rtk-exam-start')) {
            setState('أحسنت! أنهيت جميع فيديوهات المحور. يمكنك الآن بدء الاختبار.', 'success');
            exam.scrollIntoView({ behavior: 'smooth', block: 'center' });
            return true;
        }
        return false;   // لا اختبار ظاهر ولا فيديو متاح: تحميل كامل يعرض الحالة الصحيحة
    }

    // ملاذ أخير: تحميل كامل (الإتمام محفوظ في الخادم)
    function reloadToContinue() {
        setState('✓ اكتمل الفيديو. جارٍ تحديث الصفحة... <a href="' + esc(location.href) + '">اضغط هنا إن لم تتحدّث</a>', 'success');
        setTimeout(function () { location.reload(); }, 1500);
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
        setIcon(document.getElementById('rtkBtnPlay'), current.playing ? 'fa-pause' : 'fa-play');
        ping(state);
        if (state === 'playing') startTimer(); else stopTimer();
        if (state === 'ended') watchEnded(current);
    }

    // مراقب الإتمام: إن لم يصل ردّ الخادم على «ended» (انقطاع/ازدحام) أعد الإرسال ثم اعرض رابط تحديث
    function watchEnded(c) {
        var tries = 0;
        c.endedAnswered = false;
        (function check() {
            setTimeout(function () {
                if (current !== c || c.endedAnswered) return;   // اكتمل أو تغيّر الفيديو أو ردّ الخادم
                if (++tries > 3) {
                    // لا ردّ على «ended»: اسأل الخادم عن حالة القائمة فإن كان الفيديو مكتملًا هناك تابع تلقائيًا
                    syncFromServer().then(function (ok) {
                        if (current !== c) return;
                        var fresh = ok && listEl.querySelector('li[data-vp-id="' + c.id + '"]');
                        if (fresh && fresh.dataset.completed === '1') { destroyCurrent(); if (!afterSync()) reloadToContinue(); return; }
                        setState('تعذّر تأكيد إنهاء الفيديو. <a href="' + esc(location.href) + '">حدّث الصفحة</a> (مشاهدتك محفوظة).', 'danger');
                    });
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
        if (current && current.player) {
            try { if (current.provider === 'Vimeo') current.player.destroy(); else current.player.destroy(); } catch (e) { }
        }
        var host = document.getElementById('rtkPlayerHost');
        if (host) host.innerHTML = '';
        current = null;
        queued = null;
    }

    // autoplay: true عند الانتقال التلقائي من فيديو منتهٍ (بعد تفاعل الطالب مع الصفحة فيسمح المتصفح بالتشغيل)
    function select(li, autoplay) {
        if (li.dataset.unlocked !== '1' || li.dataset.completed === '1') return;
        destroyCurrent();
        var provider = li.dataset.provider;
        current = {
            li: li, id: parseInt(li.dataset.vpId, 10), provider: provider,
            player: null, playing: false, timer: null,
            maxPos: 0, dur: 0, dragging: false,   // maxPos: أبعد نقطة وصل إليها الطالب بالتشغيل الفعلي
            requiredSeconds: parseInt(li.dataset.required || '0', 10),
            autoplay: !!autoplay
        };
        renderSeek(0, 0, 0);
        [].forEach.call(listEl.querySelectorAll('.rtk-video-item'), function (x) { x.classList.remove('is-active'); });
        li.classList.add('is-active');
        if (titleEl) titleEl.textContent = li.dataset.title || '';

        setIcon(document.getElementById('rtkBtnPlay'), 'fa-play');
        setIcon(document.getElementById('rtkBtnMute'), 'fa-volume-up');
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
                playerVars: { rel: 0, modestbranding: 1, playsinline: 1, controls: 0, disablekb: 1, fs: 0, iv_load_policy: 3, origin: location.origin },
                events: {
                    onReady: function () {
                        setState('');
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
            c.poll = setInterval(function () {
                try { tick(c, c.player.getCurrentTime(), c.player.getDuration()); } catch (e) { }
            }, 500);
            c.togglePlay = function () { if (c.playing) c.player.pauseVideo(); else c.player.playVideo(); };
            c.toggleMute = function () { if (c.player.isMuted()) c.player.unMute(); else c.player.mute(); return c.player.isMuted(); };
        }).catch(function () { fail('تعذر تحميل مشغّل YouTube (قد يكون محظورًا في شبكتك). جرّب شبكة أخرى أو تواصل مع الإدارة.'); });
    }

    function mountVimeo(li) {
        var c = current;
        loadVimeo().then(function () {
            if (current !== c) return;
            var host = document.getElementById('rtkPlayerHost');
            c.player = new Vimeo.Player(host, { url: li.dataset.embedUrl, responsive: true, controls: false, title: false, byline: false, portrait: false, keyboard: false });
            c.player.ready().then(function () {
                setState('');
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
            c.player.on('timeupdate', function (d) { tick(c, d.seconds, d.duration); });
            c.togglePlay = function () { if (c.playing) c.player.pause(); else c.player.play(); };
            c.toggleMute = function () {
                c.vimeoMuted = !c.vimeoMuted;
                c.player.setMuted(c.vimeoMuted);
                return c.vimeoMuted;
            };
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

    // ───────────── حماية المشغّل: لا نقر على المنصة ولا كليك يمين ─────────────
    var shieldEl = document.getElementById('rtkPlayerShield');
    var btnPlay = document.getElementById('rtkBtnPlay');
    var btnMute = document.getElementById('rtkBtnMute');
    var btnFull = document.getElementById('rtkBtnFull');

    function setIcon(btn, cls) { var i = btn && btn.querySelector('i'); if (i) i.className = 'fas ' + cls; }
    function togglePlay() {
        if (current && current.togglePlay) { try { current.togglePlay(); } catch (e) { } }
    }

    if (wrapEl) {
        wrapEl.addEventListener('contextmenu', function (e) { e.preventDefault(); });
        wrapEl.addEventListener('dragstart', function (e) { e.preventDefault(); });
    }

    // ── منع الكليك يمين على الصفحة كاملة + اختصارات عرض المصدر/أدوات المطوّر (رادع فقط، لا يغني عن حماية الخادم) ──
    document.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    document.addEventListener('keydown', function (e) {
        var k = (e.key || '').toLowerCase();
        var ctrl = e.ctrlKey || e.metaKey;
        var devtools = k === 'f12' || (ctrl && e.shiftKey && (k === 'i' || k === 'j' || k === 'c')) || (e.metaKey && e.altKey && (k === 'i' || k === 'j' || k === 'c'));
        if (devtools || (ctrl && (k === 'u' || k === 's'))) { e.preventDefault(); e.stopPropagation(); }
    }, true);
    // ── شريط التقدّم: رجوع وتنقّل داخل ما شوهد فقط، ولا قفز للأمام ──
    var seekEl = document.getElementById('rtkSeek');
    var timeEl = document.getElementById('rtkTime');
    var SKIP_TOLERANCE = 2;   // ثوانٍ: أكبر قفزة تُعتبر تشغيلًا طبيعيًا

    function fmt(s) {
        s = Math.max(0, Math.floor(s || 0));
        var m = Math.floor(s / 60), r = s % 60;
        return m + ':' + (r < 10 ? '0' : '') + r;
    }
    function renderSeek(t, d, maxPos) {
        if (!seekEl) return;
        seekEl.value = d > 0 ? Math.min(1000, (t / d) * 1000) : 0;
        var pct = d > 0 ? Math.min(100, (maxPos / d) * 100) : 0;
        // الجزء المسموح بالتنقل فيه بلون مميز
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
        if (!c.dragging) renderSeek(t, c.dur, c.maxPos);
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

    if (shieldEl) shieldEl.addEventListener('click', togglePlay);
    if (btnPlay) btnPlay.addEventListener('click', togglePlay);
    if (btnMute) {
        btnMute.addEventListener('click', function () {
            if (!current || !current.toggleMute) return;
            try { setIcon(btnMute, current.toggleMute() ? 'fa-volume-mute' : 'fa-volume-up'); } catch (e) { }
        });
    }
    // ═════ RTK v2 — ملء الشاشة: Fullscreen API (+WebKit) ثم بديل CSS عند التعذّر (iPhone/WebView) ═════
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

    function setPseudoFs(on) {
        wrapEl.classList.toggle('rtk-pseudo-fs', on);
        document.documentElement.classList.toggle('rtk-no-scroll', on);
        setIcon(btnFull, on ? 'fa-compress' : 'fa-expand');
        try {
            if (on && screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(function () { });
            if (!on && screen.orientation && screen.orientation.unlock) screen.orientation.unlock();
        } catch (e) { }
    }

    if (btnFull && wrapEl) {
        btnFull.addEventListener('click', function () {
            if (wrapEl.classList.contains('rtk-pseudo-fs')) { setPseudoFs(false); return; }
            if (fsElement()) { exitFs(); return; }
            requestFs(wrapEl).catch(function () { setPseudoFs(true); });
        });
        ['fullscreenchange', 'webkitfullscreenchange'].forEach(function (ev) {
            document.addEventListener(ev, function () { setIcon(btnFull, fsElement() ? 'fa-compress' : 'fa-expand'); });
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && wrapEl.classList.contains('rtk-pseudo-fs')) setPseudoFs(false);
        });
    }

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
