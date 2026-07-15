// Shared show/hide-password toggle for the auth pages.
// Swaps the eye <-> eye-off icon and the aria-label (show <-> hide) to match
// the Next.js behavior. Replaces the divergent per-page inline scripts.
(function () {
    'use strict';

    function wire(btn) {
        var wrap = btn.closest('[data-password-wrap]');
        if (!wrap) return;
        var input = wrap.querySelector('input[type="password"], input[type="text"]');
        if (!input) return;

        var showIcon = btn.querySelector('[data-pw-icon="show"]');
        var hideIcon = btn.querySelector('[data-pw-icon="hide"]');
        var showLabel = btn.getAttribute('data-show-label');
        var hideLabel = btn.getAttribute('data-hide-label');

        btn.addEventListener('click', function () {
            var reveal = input.type === 'password';
            input.type = reveal ? 'text' : 'password';
            if (showIcon) showIcon.classList.toggle('hidden', reveal);
            if (hideIcon) hideIcon.classList.toggle('hidden', !reveal);
            var label = reveal ? hideLabel : showLabel;
            if (label) {
                btn.setAttribute('aria-label', label);
                btn.setAttribute('title', label);
            }
        });
    }

    function init() {
        document.querySelectorAll('[data-password-toggle]').forEach(wire);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
