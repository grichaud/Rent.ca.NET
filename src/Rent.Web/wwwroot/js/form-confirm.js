// Confirmacion de submit via data-confirm. El texto viaja por atributo (Razor lo encodea),
// no interpolado dentro de un literal JS: un apostrofo en la traduccion romperia el script.
(function () {
    'use strict';

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.dataset) return;
        var message = form.dataset.confirm;
        if (message && !window.confirm(message)) {
            e.preventDefault();
        }
    });
})();
