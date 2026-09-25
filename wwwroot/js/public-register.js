/* صفحة التسجيل العامة (RL-S4) — تحسينات فقط؛ الفورم يعمل بدون JavaScript */
(function () {
    'use strict';

    var root = document.getElementById('rlRegister');
    if (!root || root.dataset.rlBound === '1') return;
    root.dataset.rlBound = '1';
    root.classList.add('rl-js');

    var form = document.getElementById('rlForm');
    var checkboxes = Array.prototype.slice.call(root.querySelectorAll('.rl-course-cb'));
    var chips = document.getElementById('rlChips');
    var chipsEmpty = document.getElementById('rlChipsEmpty');
    var sticky = document.getElementById('rlSticky');
    var stickyText = document.getElementById('rlStickyText');
    var continueBtn = document.getElementById('rlContinue');
    var panel = document.getElementById('rlFormPanel');
    var submitBtn = document.getElementById('rlSubmit');

    function accentOf(cb) {
        var card = cb.closest('.rl-program');
        return card ? card.style.getPropertyValue('--rl-accent') : '';
    }

    function render() {
        var picked = checkboxes.filter(function (cb) { return cb.checked; });
        var programs = {};
        picked.forEach(function (cb) { programs[cb.dataset.program] = true; });
        var programCount = Object.keys(programs).length;

        // بطاقات البرامج
        root.querySelectorAll('.rl-program').forEach(function (card) {
            card.classList.toggle('has-selection', !!card.querySelector('.rl-course-cb:checked'));
        });

        // Chips
        if (chips) {
            chips.textContent = '';
            picked.forEach(function (cb) {
                var chip = document.createElement('span');
                chip.className = 'rl-chip';
                var accent = accentOf(cb);
                if (accent) chip.style.setProperty('--rl-accent', accent);

                var label = document.createElement('span');
                label.textContent = cb.dataset.name;

                var remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'rl-chip-remove';
                remove.setAttribute('aria-label', 'إزالة ' + cb.dataset.name);
                remove.textContent = '×';
                remove.addEventListener('click', function () {
                    cb.checked = false;
                    render();
                });

                chip.appendChild(label);
                chip.appendChild(remove);
                chips.appendChild(chip);
            });
        }
        if (chipsEmpty) chipsEmpty.hidden = picked.length > 0;

        // الشريط اللاصق
        if (sticky) {
            sticky.hidden = false;
            if (picked.length === 0) {
                stickyText.textContent = 'لم تختر دورات بعد';
            } else {
                stickyText.textContent = 'اخترت ' + picked.length + (picked.length === 1 ? ' دورة' : ' دورات') +
                    ' من ' + programCount + (programCount === 1 ? ' برنامج' : ' برامج');
            }
            continueBtn.disabled = picked.length === 0;
        }
    }

    // تحديد/إلغاء كل دورات برنامج (Delegation — ربط واحد)
    root.addEventListener('click', function (e) {
        var selectAll = e.target.closest('[data-select-all]');
        var clearAll = e.target.closest('[data-clear-all]');
        if (!selectAll && !clearAll) return;
        var id = (selectAll || clearAll).dataset.selectAll || (selectAll || clearAll).dataset.clearAll;
        checkboxes.forEach(function (cb) {
            if (cb.dataset.program === id) cb.checked = !!selectAll;
        });
        render();
    });

    root.addEventListener('change', function (e) {
        if (e.target.classList && e.target.classList.contains('rl-course-cb')) render();
        if (e.target.name === 'Form.ApplicantType') syncApplicant();
    });

    // صفة المُسجِّل
    function syncApplicant() {
        var parent = root.querySelector('input[name="Form.ApplicantType"]:checked');
        root.classList.toggle('is-parent', !!parent && parent.id === 'at-parent');
    }

    if (continueBtn && panel) {
        continueBtn.addEventListener('click', function () {
            panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
            var first = panel.querySelector('input:not([type=radio]):not([type=hidden])');
            if (first) setTimeout(function () { first.focus({ preventScroll: true }); }, 400);
        });

        // إخفاء الشريط عندما يظهر الفورم لتجنب تغطية زر الإرسال
        if ('IntersectionObserver' in window) {
            new IntersectionObserver(function (entries) {
                sticky.classList.toggle('is-tucked', entries[0].isIntersecting);
            }, { threshold: 0.15 }).observe(panel);
        }
    }

    // منع الضغط المزدوج
    if (form && submitBtn) {
        form.addEventListener('submit', function () {
            if (submitBtn.classList.contains('is-loading')) return;
            submitBtn.classList.add('is-loading');
            submitBtn.setAttribute('aria-busy', 'true');
        });
        // عند الرجوع بزر Back من الصفحة التالية (bfcache)
        window.addEventListener('pageshow', function (e) {
            if (e.persisted) {
                submitBtn.classList.remove('is-loading');
                submitBtn.removeAttribute('aria-busy');
            }
        });
    }

    syncApplicant();
    render();
})();
