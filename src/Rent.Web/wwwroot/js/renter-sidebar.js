(function () {
    'use strict';

    var drawer = document.getElementById('renter-sidebar-drawer');
    if (!drawer) return;

    var openBtns = document.querySelectorAll('[data-renter-sidebar-open]');
    var closeBtns = drawer.querySelectorAll('[data-renter-sidebar-close]');
    var overlay = drawer.querySelector('[data-renter-sidebar-overlay]');
    var panel = drawer.querySelector('[data-renter-sidebar-panel]');

    function open() {
        drawer.classList.remove('pointer-events-none');
        drawer.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
        requestAnimationFrame(function () {
            if (overlay) overlay.classList.add('opacity-100');
            if (panel) {
                panel.classList.remove('-translate-x-full');
                panel.classList.add('translate-x-0');
            }
        });
        openBtns.forEach(function (b) { b.setAttribute('aria-expanded', 'true'); });
    }

    function close() {
        if (overlay) overlay.classList.remove('opacity-100');
        if (panel) {
            panel.classList.add('-translate-x-full');
            panel.classList.remove('translate-x-0');
        }
        openBtns.forEach(function (b) { b.setAttribute('aria-expanded', 'false'); });
        setTimeout(function () {
            drawer.classList.add('pointer-events-none');
            drawer.setAttribute('aria-hidden', 'true');
            document.body.style.overflow = '';
        }, 300);
    }

    openBtns.forEach(function (b) { b.addEventListener('click', open); });
    closeBtns.forEach(function (b) { b.addEventListener('click', close); });
    if (overlay) overlay.addEventListener('click', close);

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && drawer.getAttribute('aria-hidden') === 'false') close();
    });
})();
