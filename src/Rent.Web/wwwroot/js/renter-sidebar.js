// Drawer de sidebar compartido por _RenterLayout y _AdminLayout (mismo id y mismos data-attributes).
(function () {
    'use strict';

    var drawer = document.getElementById('renter-sidebar-drawer');
    if (!drawer) return;

    var openBtns = document.querySelectorAll('[data-renter-sidebar-open]');
    var closeBtns = drawer.querySelectorAll('[data-renter-sidebar-close]');
    var overlay = drawer.querySelector('[data-renter-sidebar-overlay]');
    var panel = drawer.querySelector('[data-renter-sidebar-panel]');
    var lastTrigger = null;

    // Cerrado, el panel sigue en el DOM (solo desplazado): inert lo saca del orden de tabulacion.
    if (panel) panel.setAttribute('inert', '');

    function open(e) {
        lastTrigger = (e && e.currentTarget) || null;
        drawer.classList.remove('pointer-events-none');
        drawer.setAttribute('aria-hidden', 'false');
        if (panel) panel.removeAttribute('inert');
        document.body.style.overflow = 'hidden';
        requestAnimationFrame(function () {
            if (overlay) overlay.classList.add('opacity-100');
            if (panel) {
                panel.classList.remove('-translate-x-full');
                panel.classList.add('translate-x-0');
            }
        });
        openBtns.forEach(function (b) { b.setAttribute('aria-expanded', 'true'); });
        if (closeBtns.length) closeBtns[0].focus();
    }

    function close() {
        if (overlay) overlay.classList.remove('opacity-100');
        if (panel) {
            panel.classList.add('-translate-x-full');
            panel.classList.remove('translate-x-0');
            panel.setAttribute('inert', '');
        }
        drawer.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        openBtns.forEach(function (b) { b.setAttribute('aria-expanded', 'false'); });
        if (lastTrigger) { lastTrigger.focus(); lastTrigger = null; }
        // Solo lo diferido: si se reabrio dentro de la animacion, no lo desactives.
        setTimeout(function () {
            if (drawer.getAttribute('aria-hidden') === 'true') drawer.classList.add('pointer-events-none');
        }, 300);
    }

    openBtns.forEach(function (b) { b.addEventListener('click', open); });
    closeBtns.forEach(function (b) { b.addEventListener('click', close); });
    if (overlay) overlay.addEventListener('click', close);

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && drawer.getAttribute('aria-hidden') === 'false') close();
    });
})();
