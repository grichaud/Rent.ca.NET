(function () {
    'use strict';

    /* ---------------------------------------------------------------------- *
     *  Dual-range price slider
     * ---------------------------------------------------------------------- */
    function initPriceSliders() {
        document.querySelectorAll('[data-price-slider]').forEach(function (root) {
            var minInput = root.querySelector('[data-price-input-min]');
            var maxInput = root.querySelector('[data-price-input-max]');
            var hiddenMax = root.querySelector('[data-price-hidden-max]');
            var labelMin = root.querySelector('[data-price-label-min]');
            var labelMax = root.querySelector('[data-price-label-max]');
            var summary  = root.querySelector('[data-price-summary]');
            var track    = root.querySelector('[data-price-track]');
            if (!minInput || !maxInput || !track) return;

            var cap = parseInt(root.getAttribute('data-price-cap') || '5000', 10);
            var rangeMin = parseInt(minInput.min, 10);
            var rangeMax = parseInt(minInput.max, 10);

            function fmt(v) {
                if (v >= cap) return '$' + cap.toLocaleString('en-CA') + '+';
                return '$' + v.toLocaleString('en-CA');
            }

            function update() {
                var lo = parseInt(minInput.value, 10);
                var hi = parseInt(maxInput.value, 10);
                if (lo > hi - 50) {
                    if (this === maxInput) lo = Math.max(rangeMin, hi - 50);
                    else hi = Math.min(rangeMax, lo + 50);
                    minInput.value = lo;
                    maxInput.value = hi;
                }
                var loPct = ((lo - rangeMin) / (rangeMax - rangeMin)) * 100;
                var hiPct = ((hi - rangeMin) / (rangeMax - rangeMin)) * 100;
                track.style.left = loPct + '%';
                track.style.right = (100 - hiPct) + '%';
                if (labelMin) labelMin.textContent = fmt(lo);
                if (labelMax) labelMax.textContent = fmt(hi);
                if (summary)  summary.textContent  = fmt(lo) + ' – ' + fmt(hi) + ' per month';

                if (hiddenMax) hiddenMax.value = hi >= cap ? '' : String(hi);
                if (lo === 0) {
                    minInput.removeAttribute('name');
                } else if (!minInput.hasAttribute('name')) {
                    minInput.setAttribute('name', 'MinPrice');
                }
                // The MaxPriceRaw field is decorative (its value drives the UI); the real
                // field submitted is the hidden one. Strip the name so it doesn't post.
                maxInput.removeAttribute('name');
            }

            minInput.addEventListener('input', update);
            maxInput.addEventListener('input', update);
            update();
        });
    }

    /* ---------------------------------------------------------------------- *
     *  Sort dropdown auto-submit
     * ---------------------------------------------------------------------- */
    function initSortDropdown() {
        document.querySelectorAll('[data-sort-select]').forEach(function (sel) {
            sel.addEventListener('change', function () {
                var header = sel.closest('[data-results-header]');
                var base = header && header.getAttribute('data-base-url') || window.location.pathname;
                var params = new URLSearchParams(window.location.search);
                params.set('Sort', sel.value);
                window.location.href = base + '?' + params.toString();
            });
        });
    }

    /* ---------------------------------------------------------------------- *
     *  View mode toggle (Grid / List / Map)
     * ---------------------------------------------------------------------- */
    function initViewToggle() {
        document.querySelectorAll('[data-view-btn]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var view = btn.getAttribute('data-view-btn');
                var header = btn.closest('[data-results-header]');
                var base = header && header.getAttribute('data-base-url') || window.location.pathname;
                var params = new URLSearchParams(window.location.search);
                params.set('View', view);
                window.location.href = base + '?' + params.toString();
            });
        });
    }

    /* ---------------------------------------------------------------------- *
     *  More-filters collapsible
     * ---------------------------------------------------------------------- */
    function initMoreFilters() {
        document.querySelectorAll('[data-more-filters]').forEach(function (root) {
            var toggle = root.querySelector('[data-more-toggle]');
            var content = root.querySelector('[data-more-content]');
            var chevron = toggle && toggle.querySelector('svg');
            if (!toggle || !content) return;
            function apply() {
                var open = root.getAttribute('data-open') === 'true';
                content.style.display = open ? '' : 'none';
                if (chevron) chevron.style.transform = open ? 'rotate(180deg)' : '';
                toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            }
            toggle.addEventListener('click', function () {
                root.setAttribute('data-open', root.getAttribute('data-open') === 'true' ? 'false' : 'true');
                apply();
            });
            apply();
        });
    }

    /* ---------------------------------------------------------------------- *
     *  Mobile filter drawer
     * ---------------------------------------------------------------------- */
    function initMobileDrawer() {
        var openBtns = document.querySelectorAll('[data-mobile-filter-open]');
        var drawer = document.getElementById('rentca-mobile-filters');
        if (!drawer) return;
        var overlay = drawer.querySelector('[data-mobile-filter-overlay]');
        var panel = drawer.querySelector('[data-mobile-filter-panel]');

        function open() {
            drawer.classList.remove('pointer-events-none');
            drawer.removeAttribute('aria-hidden');
            requestAnimationFrame(function () {
                if (overlay) overlay.style.opacity = '1';
                if (panel) panel.style.transform = 'translateX(0)';
            });
            document.body.style.overflow = 'hidden';
        }
        function close() {
            if (overlay) overlay.style.opacity = '0';
            if (panel) panel.style.transform = 'translateX(-100%)';
            setTimeout(function () {
                drawer.classList.add('pointer-events-none');
                drawer.setAttribute('aria-hidden', 'true');
            }, 300);
            document.body.style.overflow = '';
        }
        openBtns.forEach(function (b) { b.addEventListener('click', open); });
        if (overlay) overlay.addEventListener('click', close);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && drawer.getAttribute('aria-hidden') !== 'true') close();
        });
    }

    /* ---------------------------------------------------------------------- *
     *  Search bar: clear button + slugify-and-navigate submit
     * ---------------------------------------------------------------------- */
    function slugify(text) {
        return text.toLowerCase()
            .normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/(^-|-$)+/g, '');
    }

    function initSearchBar() {
        document.querySelectorAll('[data-search-bar]').forEach(function (form) {
            var input = form.querySelector('[data-search-input]');
            var clear = form.querySelector('[data-search-clear]');
            if (!input) return;

            // Intercept submit: navigate to /{culture}/{slug} of the typed city
            // (mirrors the hero search on the home page).
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                var culture = form.getAttribute('data-culture') ||
                    (window.location.pathname.split('/')[1] || 'en');
                var slug = slugify((input.value || '').trim());
                if (slug) window.location.href = '/' + culture + '/' + slug;
            });

            if (!clear) return;
            function sync() { clear.classList.toggle('hidden', !input.value); }
            input.addEventListener('input', sync);
            clear.addEventListener('click', function () {
                input.value = '';
                sync();
                input.focus();
            });
            sync();
        });
    }

    function init() {
        initPriceSliders();
        initSortDropdown();
        initViewToggle();
        initMoreFilters();
        initMobileDrawer();
        initSearchBar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
