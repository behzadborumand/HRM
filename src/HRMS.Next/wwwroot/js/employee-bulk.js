(() => {
    'use strict';
    const form = document.querySelector('[data-bulk-selection]');
    if (form) {
        const all = form.querySelector('[data-select-all]');
        const rows = [...form.querySelectorAll('[data-select-employee]')];
        const submit = form.querySelector('[data-bulk-submit]');
        const status = form.querySelector('[data-selection-count]');
        const update = () => {
            const count = rows.filter(row => row.checked).length;
            all.checked = rows.length > 0 && count === rows.length;
            all.indeterminate = count > 0 && count < rows.length;
            all.disabled = rows.length === 0;
            submit.disabled = count === 0;
            status.textContent = new Intl.NumberFormat('fa-IR').format(count) + ' کارمند در این صفحه انتخاب شده';
        };
        all.addEventListener('change', () => { rows.forEach(row => { row.checked = all.checked; }); update(); });
        rows.forEach(row => row.addEventListener('change', update));
        form.addEventListener('submit', event => { if (!rows.some(row => row.checked)) event.preventDefault(); });
        window.addEventListener('pageshow', update);
        update();
    }
    const preview = document.querySelector('#bulkPreview');
    if (preview) {
        const fields = [...document.querySelectorAll('[data-preview]')];
        const updatePreview = () => {
            preview.replaceChildren();
            const selected = fields.filter(field => field.value !== '');
            const lines = selected.length ? selected.map(field => field.dataset.preview + ': ' + field.selectedOptions[0].textContent) : ['تغییری انتخاب نشده است.'];
            lines.forEach(text => { const item = document.createElement('li'); item.textContent = text; preview.appendChild(item); });
        };
        fields.forEach(field => field.addEventListener('change', updatePreview));
        updatePreview();
    }
})();