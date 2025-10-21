const PokemonIndex = (() => {
    let form, listContainer, btnExport, btnEmailAll, alerts;

    function getToken() {
        const el = form.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    async function loadSpecies() {
        // TODO: Llamar al endpoint MVC que ya consume la PokeAPI (capa de servicio)
        const url = form.dataset.listUrl.replace('List', 'Species');
        const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) return;

        const species = await res.json(); // [{id:1, name:'bulbasaur'}, ...]
        const sel = document.getElementById('speciesSelect');
        species.forEach(s => {
            const opt = document.createElement('option');
            opt.value = s.id;
            opt.textContent = capitalize(s.name);
            sel.appendChild(opt);
        });
    }

    function readFilters() {
        const data = new FormData(form);
        return new URLSearchParams(data);
    }

    async function fetchList() {
        const url = form.dataset.listUrl;
        const body = readFilters();

        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest',
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body
        });

        if (!res.ok) {
            showError('No se pudo cargar la lista.');
            return;
        }

        const html = await res.text();
        listContainer.innerHTML = html;
        wireListEvents();
        toggleBulkButtons();
    }

    function wireListEvents() {
        // Paginación
        listContainer.querySelectorAll('.js-page').forEach(a => {
            a.addEventListener('click', e => {
                e.preventDefault();
                const page = Number(a.dataset.page);
                if (!page) return;
                form.querySelector('input[name="Page"]').value = page;
                fetchList();
            });
        });

        // Envío individual
        listContainer.querySelectorAll('.js-email-one').forEach(btn => {
            btn.addEventListener('click', async () => {
                await sendEmail(btn.dataset.emailUrl);
            });
        });
    }

    async function sendEmail(url) {
        setLoading(true, 'Enviando correo...');
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest'
            }
        });
        setLoading(false);

        if (!res.ok) return showError('No se pudo enviar el correo.');
        showSuccess('Correo enviado.');
    }

    function toggleBulkButtons() {
        // Si hay filas, habilita export y correo general
        const hasRows = listContainer.querySelector('tbody tr');
        btnExport.disabled = !hasRows;
        btnEmailAll.disabled = !hasRows;
    }

    async function exportExcel() {
        const url = form.dataset.exportUrl;
        const body = readFilters();

        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest',
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body
        });

        if (!res.ok) return showError('No se pudo exportar.');
        const blob = await res.blob();
        const a = document.createElement('a');
        const urlBlob = URL.createObjectURL(blob);
        a.href = urlBlob;
        a.download = 'pokemon.xlsx';
        a.click();
        URL.revokeObjectURL(urlBlob);
    }

    async function emailAll() {
        const url = form.dataset.emailAllUrl;
        const body = readFilters();

        setLoading(true, 'Enviando correos...');
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest',
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body
        });
        setLoading(false);

        if (!res.ok) return showError('No se pudo enviar correos.');
        showSuccess('Correos enviados.');
    }

    function wireHeaderEvents() {
        // Buscar
        form.addEventListener('submit', e => {
            e.preventDefault();
            form.querySelector('input[name="Page"]').value = 1;
            fetchList();
        });

        // Limpiar
        document.getElementById('btnClear').addEventListener('click', () => {
            form.reset();
            form.querySelector('input[name="Page"]').value = 1;
            fetchList();
        });

        // Exportar
        btnExport.addEventListener('click', exportExcel);

        // Enviar correo a toda la lista
        btnEmailAll.addEventListener('click', emailAll);
    }

    function setLoading(isLoading, message = 'Cargando...') {
        if (isLoading) {
            alerts.innerHTML = `<div class="alert alert-info py-2">${message}</div>`;
        } else {
            alerts.innerHTML = '';
        }
    }

    function showError(msg) {
        alerts.innerHTML = `<div class="alert alert-danger py-2">${msg}</div>`;
    }

    function showSuccess(msg) {
        alerts.innerHTML = `<div class="alert alert-success py-2">${msg}</div>`;
    }

    function capitalize(s) {
        return (s || '').charAt(0).toUpperCase() + (s || '').slice(1);
    }

    async function init() {
        form = document.getElementById('filtersForm');
        listContainer = document.getElementById('pokemonList');
        btnExport = document.getElementById('btnExport');
        btnEmailAll = document.getElementById('btnEmailAll');
        alerts = document.getElementById('alerts');

        wireHeaderEvents();
        await loadSpecies();
        await fetchList();
    }

    return { init };
})();
