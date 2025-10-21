// wwwroot/js/views/pokemon.index.js
window.PokemonIndex = (function () {
    let $form, $list, $alerts;
    let urls = {};
    let token = "";

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

    async function loadSpecies() {
        try {
            const url = urls.species; // MVC: /Pokemon/Species => [{id,name}]
            const res = await fetch(url, { method: "GET" });
            if (!res.ok) throw new Error("No se pudo cargar Species");
            const data = await res.json();
            const $sel = $("#speciesSelect");
            $sel.find("option:not([value=''])").remove();
            data.forEach(x => $sel.append(new Option(cap(x.name), x.id)));
        } catch (err) {
            showAlert("warning", "No se pudo cargar el catálogo de especies.");
        }
    }

    async function loadList() {
        setLoading(true);
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

    // Helpers
    function setLoading(isLoading) {
        if (isLoading) {
            $list.html(`
        <div class="card"><div class="card-body">
          <div class="text-muted">Cargando...</div>
        </div></div>`);
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

    return { init };
})();
