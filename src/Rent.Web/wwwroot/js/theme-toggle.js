(function () {
  const STORAGE_KEY = 'rentca-theme';
  const COOKIE_NAME = 'rentca-theme';
  const root = document.documentElement;

  function setCookie(value) {
    const oneYear = 60 * 60 * 24 * 365;
    document.cookie = `${COOKIE_NAME}=${value}; path=/; max-age=${oneYear}; SameSite=Lax`;
  }

  function apply(theme) {
    if (theme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
  }

  function current() {
    return root.classList.contains('dark') ? 'dark' : 'light';
  }

  function preferred() {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'dark' || stored === 'light') return stored;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  function toggle() {
    const next = current() === 'dark' ? 'light' : 'dark';
    apply(next);
    localStorage.setItem(STORAGE_KEY, next);
    setCookie(next);
    document.dispatchEvent(new CustomEvent('themechange', { detail: { theme: next } }));
  }

  apply(preferred());
  setCookie(current());

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
      btn.addEventListener('click', toggle);
    });
  });

  window.RentCaTheme = { toggle: toggle, current: current };
})();
