// wwwroot/js/views/pokemon.index.js
// =============================================================================
//  Módulo principal: PokemonIndex
//  Controla filtros, lista, exportación y envío de correos
// =============================================================================

window.PokemonIndex = (function () {

    // #region === VARIABLES GLOBALES ===
    let $form, $list, $alerts;
    let urls = {};
    let token = "";
    // #endregion


    // #region === INICIALIZACIÓN ===
    function init() {
        $form = $("#filtersForm");
        $list = $("#pokemonList");
        $alerts = $("#alerts");

        urls.list = $form.data("list-url");
        urls.export = $form.data("export-url");
        urls.emailAll = $form.data("email-all-url");
        urls.species = $form.data("species-url");
        token = $form.find('input[name="__RequestVerificationToken"]').val();

        bindEvents();
        loadSpecies().then(loadList);
    }
    // #endregion


    // #region === EVENTOS PRINCIPALES ===
    function bindEvents() {
        // Buscar
        $form.on("submit", function (e) {
            e.preventDefault();
            // Siempre arranca desde página 1 al buscar
            $form.find('input[name="Page"]').val(1);
            loadList();
        });

        // Limpiar
        $("#btnClear").on("click", function () {
            $form[0].reset();
            $form.find('input[name="Page"]').val(1);
            loadList();
        });

        // Paginación (delegada desde el contenedor)
        $list.on("click", ".js-page", function (e) {
            e.preventDefault();
            const page = $(this).data("page");
            if (!page) return;
            $form.find('input[name="Page"]').val(page);
            loadList();
        });

        // Exportar
        $("#btnExport").on("click", exportExcel);

        // Enviar correos a la lista actual
        $("#btnEmailAll").on("click", sendAllEmails);

        // Enviar correo a uno (delegado, botones que vienen en el partial)
        $list.on("click", ".js-email-one", sendOneEmail);
    }
    // #endregion


    // #region === CARGA DE CATÁLOGO (ESPECIES) ===
    async function loadSpecies() {
        try {
            setLoading(true);
            const url = urls.species; 
            const res = await fetch(url, { method: "GET" });
            if (!res.ok) throw new Error("No se pudo cargar Species");
            const data = await res.json();
            const $sel = $("#speciesSelect");
            $sel.find("option:not([value=''])").remove();
            data.forEach(x => $sel.append(new Option(cap(x.name), x.id)));
        } catch (err) {
            showAlert("warning", "No se pudo cargar el catálogo de especies.");
            setLoading(false)
        }
    }
    // #endregion


    // #region === LISTA PRINCIPAL ===
    async function loadList() {
        disableActions(true);

        try {
            const formData = new FormData($form[0]);
            const res = await fetch(urls.list, {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body: formData
            });

            // 499 = cancelado por cliente (convención), lo ignoramos
            if (res.status === 499) return;
            if (!res.ok) throw new Error("Error al cargar la lista");

            const html = await res.text();
            $list.html(html);

            // Habilitar acciones si hay filas
            const hasRows = $list.find("tbody tr").length > 0;
            disableActions(!hasRows);
        } catch (err) {
            showAlert("danger", "Ocurrió un error al cargar la lista.");
        } finally {
            setLoading(false);
        }
    }
    // #endregion


    // #region === EXPORTACIÓN (EXCEL/CSV) ===
    async function exportExcel() {
        try {
            const formData = new FormData($form[0]);
            const res = await fetch(urls.export, {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body: formData
            });
            if (!res.ok) throw new Error("No se pudo exportar.");

            const blob = await res.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            // Intenta deducir el nombre; por defecto usamos pokemon.xlsx
            a.download = getFileNameFromResponse(res) || "pokemon.xlsx";
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
        } catch {
            showAlert("danger", "No se pudo exportar el Excel.");
        }
    }
    // #endregion


    // #region === ENVÍO DE CORREOS ===
    async function sendAllEmails() {
        try {
            const formData = objectFromForm($form[0]); // Plain object del filtro
            const res = await fetch(urls.emailAll, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token
                },
                body: JSON.stringify(formData)
            });
            if (!res.ok) throw new Error();

            const text = await res.text(); // Ok("...") devuelve texto plano
            showAlert("success", text || "Correos enviados.");
        } catch {
            showAlert("danger", "No se pudieron enviar los correos.");
        }
    }

    async function sendOneEmail(e) {
        e.preventDefault();
        const url = e.currentTarget.getAttribute("data-email-url");
        if (!url) return;

        try {
            const res = await fetch(url, {
                method: "POST",
                headers: { "RequestVerificationToken": token }
            });
            if (!res.ok) throw new Error();
            const text = await res.text();
            showAlert("success", text || "Correo enviado.");
        } catch {
            showAlert("danger", "No se pudo enviar el correo.");
        }
    }
    // #endregion


    // #region === HELPERS VISUALES ===
    function setLoading(isLoading) {
        if (isLoading) {
            $list.html(`
            <div style="position:relative;min-height:150px;display:grid;place-items:center;">
                <div style="position:absolute;inset:0;background:rgba(0,0,0,.25);backdrop-filter:blur(2px);"></div>
                <div style="position:relative;min-width:220px;max-width:90%;
                            padding:16px 18px;border-radius:12px;
                            background:rgba(20,24,35,.85);
                            border:1px solid rgba(255,255,255,.08);
                            box-shadow:0 8px 24px rgba(0,0,0,.35);
                            text-align:center;">
                    <div class="spinner-border text-light" role="status" aria-hidden="true"
                         style="width:2.5rem;height:2.5rem;margin:2px auto 10px;display:block;"></div>
                    <div class="fw-semibold" style="color:#b9d3ff;letter-spacing:.2px;">
                        Cargando...
                    </div>
                </div>
            </div>
        `);
        }
    }


    function disableActions(disabled) {
        $("#btnExport").prop("disabled", disabled);
        $("#btnEmailAll").prop("disabled", disabled);
    }

    function showAlert(type, message) {
        const id = "al" + Date.now();
        $alerts.html(`
      <div id="${id}" class="alert alert-${type} alert-dismissible fade show" role="alert">
        ${escapeHtml(message)}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
      </div>`);
    }
    // #endregion


    // #region === UTILIDADES GENERALES ===
    function objectFromForm(form) {
        const fd = new FormData(form);
        const obj = {};
        fd.forEach((v, k) => {
            // Convierte números cuando aplica
            if (k === "Page" || k === "PageSize" || k === "SpeciesId") {
                const n = (v ?? "").toString().trim();
                obj[k] = n === "" ? null : Number(n);
            } else {
                obj[k] = v;
            }
        });
        return obj;
    }

    function cap(s) {
        return (s || "").toString().toLowerCase().replace(/^\w/, c => c.toUpperCase());
    }

    function escapeHtml(s) {
        return (s || "").toString()
            .replace(/&/g, "&amp;").replace(/</g, "&lt;")
            .replace(/>/g, "&gt;").replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function getFileNameFromResponse(res) {
        const disp = res.headers.get("Content-Disposition");
        if (!disp) return null;
        const m = /filename\*=UTF-8''([^;]+)|filename="?([^\";]+)"?/i.exec(disp);
        return decodeURIComponent(m?.[1] || m?.[2] || "");
    }
    // #endregion


    // #region === EXPORTAR MÓDULO ===
    return { init };
    // #endregion

})();
