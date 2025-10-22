// =============================================================================
//  Módulo principal: PokemonIndex
//  Controla filtros, lista y exportación
// =============================================================================

window.PokemonIndex = (function () {

    // #region === VARIABLES GLOBALES ===
    let $form, $list, $alerts;
    let urls = {};
    let token = "";
    let isBusy = false;
    // #endregion


    // #region === INICIALIZACIÓN ===
    function init() {
        $form = $("#filtersForm");
        $list = $("#pokemonList");
        $alerts = $("#alerts");

        urls.list = $form.data("list-url");
        urls.export = $form.data("export-url");
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
            if (isBusy || $(this).hasClass("disabled")) return;
            const page = $(this).data("page");
            if (!page) return;
            $form.find('input[name="Page"]').val(page);
            loadList();
        });

        // Exportar
        $("#btnExport").on("click", exportExcel);
    }
    // #endregion


    // #region === CARGA DE CATÁLOGO (ESPECIES) ===
    async function loadSpecies() {
        if (isBusy) return;
        setLoading(true);
        disableActions(true);
        try {
            const res = await fetch(urls.species, { method: "GET" });
            if (!res.ok) throw new Error("No se pudo cargar Species");
            const data = await res.json();
            const $sel = $("#speciesSelect");
            $sel.find("option:not([value=''])").remove();
            data.forEach(x => $sel.append(new Option(cap(x.name), x.id)));
        } catch {
            showAlert("warning", "No se pudo cargar el catálogo de especies.");
        } finally {
            setLoading(false);
            disableActions(false);
        }
    }
    // #endregion


    // #region === LISTA PRINCIPAL ===
    async function loadList() {
        if (isBusy) return;
        isBusy = true;

        // Serializa ANTES de deshabilitar para no perder filtros
        const formData = new FormData($form[0]);

        disableActions(true);
        setLoading(true);

        try {
            const res = await fetch(urls.list, {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body: formData
            });

            if (res.status === 499) return;
            if (!res.ok) throw new Error("Error al cargar la lista");

            const html = await res.text();
            $list.html(html);

            const hasRows = $list.find("tbody tr").length > 0;
            disableActions(!hasRows ? true : false);
        } catch {
            showAlert("danger", "Ocurrió un error al cargar la lista.");
        } finally {
            setLoading(false);
            isBusy = false;
            disableActions(false);
        }
    }
    // #endregion


    // #region === EXPORTACIÓN (EXCEL/CSV) ===
    async function exportExcel() {
        if (isBusy) return;
        isBusy = true;
        disableActions(true);

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
            a.download = getFileNameFromResponse(res) || "pokemon.xlsx";
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
        } catch {
            showAlert("danger", "No se pudo exportar el Excel.");
        } finally {
            isBusy = false;
            disableActions(false);
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
        $("#btnClear").prop("disabled", disabled);
        $("#btnSearch").prop("disabled", disabled);
        $list.find(".js-page").toggleClass("disabled", disabled);
    }

    function showAlert(type, message) {
        if (window.UI?.toast) {
            window.UI.toast(type, message);
        } else {
            alert(message);
        }
    }
    // #endregion


    // #region === UTILIDADES GENERALES ===
    function objectFromForm(form) {
        const fd = new FormData(form);
        const obj = {};
        fd.forEach((v, k) => {
            if (k === "Page" || k === "PageSize" || k === "SpeciesId") {
                const n = (v ?? "").toString().trim();
                obj[k] = n === "" ? null : Number(n);
            } else {
                obj[k] = v;
            }
        });
        return obj;
    }
    // #endregion

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
