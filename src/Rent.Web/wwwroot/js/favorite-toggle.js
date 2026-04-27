(function () {
    'use strict';

    function getCsrfToken() {
        var input = document.querySelector('#rentca-csrf input[name="__RequestVerificationToken"]');
        if (input && input.value) return input.value;
        var any = document.querySelector('input[name="__RequestVerificationToken"]');
        return any ? any.value : '';
    }

    function setVisualState(btn, favorited) {
        btn.setAttribute('data-favorited', favorited ? 'true' : 'false');
        btn.setAttribute('aria-pressed', favorited ? 'true' : 'false');
        var svg = btn.querySelector('svg');
        if (svg) {
            if (favorited) {
                svg.setAttribute('fill', 'currentColor');
            } else {
                svg.setAttribute('fill', 'none');
            }
        }
        if (favorited) {
            btn.classList.add('text-rose-500');
            btn.classList.remove('text-slate-700', 'dark:text-white/80');
        } else {
            btn.classList.remove('text-rose-500');
            btn.classList.add('text-slate-700', 'dark:text-white/80');
        }
    }

    function bindButtons() {
        document.querySelectorAll('[data-favorite-toggle]').forEach(function (btn) {
            if (btn.dataset.favoriteBound === '1') return;
            btn.dataset.favoriteBound = '1';

            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                var propertyId = btn.getAttribute('data-property-id');
                if (!propertyId) return;

                var token = getCsrfToken();
                var form = btn.closest('form');
                if (form) {
                    var t = form.querySelector('input[name="__RequestVerificationToken"]');
                    if (t && t.value) token = t.value;
                }

                var fd = new FormData();
                fd.append('propertyId', propertyId);
                if (token) fd.append('__RequestVerificationToken', token);

                btn.disabled = true;

                fetch('/renter/favorites/toggle', {
                    method: 'POST',
                    headers: { 'Accept': 'application/json' },
                    body: fd,
                    credentials: 'same-origin',
                    redirect: 'manual'
                }).then(function (r) {
                    if (r.type === 'opaqueredirect' || r.status === 401 || r.status === 403) {
                        var here = window.location.pathname + window.location.search;
                        window.location.href = '/login?returnUrl=' + encodeURIComponent(here);
                        return null;
                    }
                    if (!r.ok) throw new Error('Toggle failed: ' + r.status);
                    return r.json();
                }).then(function (data) {
                    if (data && typeof data.favorited === 'boolean') {
                        setVisualState(btn, data.favorited);
                    }
                }).catch(function (err) {
                    console.error('[favorite-toggle]', err);
                }).finally(function () {
                    btn.disabled = false;
                });
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindButtons);
    } else {
        bindButtons();
    }
})();
