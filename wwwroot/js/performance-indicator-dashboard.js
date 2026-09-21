    const piCards = Array.from(document.querySelectorAll('.pi-card'));
    let currentPiFilter = 'all';

    function setPiFilter(btn) {
        document.querySelectorAll('.pi-filter-btn').forEach(function (b) {
            b.classList.remove('active');
        });

    btn.classList.add('active');
    currentPiFilter = btn.dataset.filter;
    applyPiFilters();
    }

    function applyPiFilters() {
        const searchInput = document.getElementById('pi-search-input');
    const sortSelect = document.getElementById('pi-sort-sel');
    const grid = document.getElementById('pi-grid');
    const countShown = document.getElementById('pi-count-shown');
    const empty = document.getElementById('pi-empty');

    const q = searchInput ? searchInput.value.toLowerCase().trim() : '';
    const sort = sortSelect ? sortSelect.value : 'date-desc';

    let visible = piCards.filter(function (card) {
        let matchFilter = false;

    if (currentPiFilter === 'all') {
        matchFilter = true;
            } else if (currentPiFilter === 'completed') {
        matchFilter = card.dataset.status === 'completed';
            } else {
        matchFilter = card.dataset.level === currentPiFilter;
            }

            const matchSearch = !q || (card.dataset.title || '').indexOf(q) >= 0;

    return matchFilter && matchSearch;
        });

    visible.sort(function (a, b) {
            if (sort === 'date-desc') {
                return (b.dataset.date || '').localeCompare(a.dataset.date || '');
            }

    if (sort === 'date-asc') {
                return (a.dataset.date || '').localeCompare(b.dataset.date || '');
            }

    if (sort === 'score-desc') {
                return (+b.dataset.score || 0) - (+a.dataset.score || 0);
            }

    if (sort === 'score-asc') {
                return (+a.dataset.score || 0) - (+b.dataset.score || 0);
            }

    if (sort === 'title-asc') {
                return (a.dataset.title || '').localeCompare(b.dataset.title || '', 'ar');
            }

    return 0;
        });

    const visibleSet = new Set(visible);

    piCards.forEach(function (card) {
            if (visibleSet.has(card)) {
        card.style.display = '';
    grid.appendChild(card);
            } else {
        card.style.display = 'none';
            }
        });

    if (countShown) {
        countShown.textContent = visible.length;
        }

    if (empty) {
        empty.style.display = visible.length ? 'none' : 'block';
        }
    }

    (function () {
        document.querySelectorAll('.pi-score-fill').forEach(function (el) {
            const target = el.style.width;
            el.style.width = '0';

            requestAnimationFrame(function () {
                requestAnimationFrame(function () {
                    el.style.width = target;
                });
            });
        });

    applyPiFilters();
    })();
