(function () {
  function init() {
    const drawer = document.getElementById('mobile-drawer');
    if (!drawer) return;

    const overlay = drawer.querySelector('[data-mobile-menu-overlay]');
    const panel = drawer.querySelector('[data-mobile-menu-panel]');
    const openButtons = document.querySelectorAll('[data-mobile-menu-open]');
    const closeButtons = drawer.querySelectorAll('[data-mobile-menu-close]');

    function open() {
      drawer.setAttribute('aria-hidden', 'false');
      drawer.classList.remove('pointer-events-none');
      overlay.classList.remove('opacity-0');
      panel.classList.remove('translate-x-full');
      document.body.style.overflow = 'hidden';
      openButtons.forEach(b => b.setAttribute('aria-expanded', 'true'));
    }

    function close() {
      drawer.setAttribute('aria-hidden', 'true');
      overlay.classList.add('opacity-0');
      panel.classList.add('translate-x-full');
      document.body.style.overflow = '';
      openButtons.forEach(b => b.setAttribute('aria-expanded', 'false'));
      window.setTimeout(function () {
        if (drawer.getAttribute('aria-hidden') === 'true') drawer.classList.add('pointer-events-none');
      }, 300);
    }

    openButtons.forEach(b => b.addEventListener('click', open));
    closeButtons.forEach(b => b.addEventListener('click', close));
    overlay.addEventListener('click', close);
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && drawer.getAttribute('aria-hidden') === 'false') close();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
