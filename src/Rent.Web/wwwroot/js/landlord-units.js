// Filas de unidades del formulario de listing. El binding de Razor exige indices
// contiguos (Input.Units[0], [1], ...), asi que se renumera tras cada alta/baja.
(function () {
    'use strict';

    var list = document.querySelector('[data-units-list]');
    if (!list) return;

    var addBtn = document.querySelector('[data-unit-add]');
    var legendTemplate = list.dataset.legendTemplate || 'Unit {0}';

    function renumber() {
        var rows = list.querySelectorAll('[data-unit-row]');
        rows.forEach(function (row, i) {
            row.querySelectorAll('[data-unit-field]').forEach(function (field) {
                var name = field.getAttribute('data-unit-field');
                field.name = 'Input.Units[' + i + '].' + name;
                if (field.id) field.id = 'unit-' + i + '-' + name.toLowerCase();
            });
            row.querySelectorAll('label[for]').forEach(function (label, j) {
                var input = row.querySelectorAll('input:not([type=hidden])')[j];
                if (input && input.id) label.setAttribute('for', input.id);
            });
            var legend = row.querySelector('[data-unit-legend]');
            if (legend) legend.textContent = legendTemplate.replace('{0}', i + 1);
            var removeBtn = row.querySelector('[data-unit-remove]');
            if (removeBtn) removeBtn.classList.toggle('hidden', rows.length === 1);
        });
    }

    if (addBtn) {
        addBtn.addEventListener('click', function () {
            var rows = list.querySelectorAll('[data-unit-row]');
            var clone = rows[rows.length - 1].cloneNode(true);
            // La copia es una unidad NUEVA: sin Id, no debe pisar la fila clonada.
            clone.querySelectorAll('[data-unit-field]').forEach(function (field) {
                if (field.getAttribute('data-unit-field') === 'Id') field.value = '';
                else if (field.type === 'number' || field.type === 'date') field.value = '';
            });
            list.appendChild(clone);
            renumber();
            var first = clone.querySelector('input:not([type=hidden])');
            if (first) first.focus();
        });
    }

    list.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-unit-remove]');
        if (!btn) return;
        var rows = list.querySelectorAll('[data-unit-row]');
        if (rows.length === 1) return; // siempre queda al menos una
        btn.closest('[data-unit-row]').remove();
        renumber();
    });

    renumber();
})();
