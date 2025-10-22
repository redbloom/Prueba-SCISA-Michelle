// =============================================================================
//  Módulo principal: PokemonEmail
//  Maneja modal de confirmación y envío por correo
// =============================================================================

window.PokemonEmail = (function () {

    // #region === VARIABLES Y CONSTANTES ===
    let $modal, modal, $form, $filters,
        $titlePrefix, $targetName, $desc, $help,
        $emailTo, $mode, $singleId, $btnConfirm;

    const urls = {
        sendOne: "/Pokemon/SendToEmail",
        sendAll: null
    };

    let isBusy = false;
    // #endregion


    // #region === INICIALIZACIÓN ===
    function init() {
        $modal = $("#emailConfirmModal");
        $form = $("#emailConfirmForm");
        $filters = $("#filtersForm");
        $titlePrefix = $("#emailTitlePrefix");
        $targetName = $("#emailTargetName");
        $desc = $("#emailDescription");
        $help = $("#emailHelp");
        $emailTo = $("#emailTo");
        $mode = $("#emailMode");
        $singleId = $("#emailSingleId");
        $btnConfirm = $("#btnConfirmSend");

        urls.sendAll = $filters.data("email-all-url") || "/Pokemon/SendAllToEmail";

        modal = bootstrap.Modal.getOrCreateInstance($modal[0]);

        bindEvents();
    }
    // #endregion


    // #region === EVENTOS ===
    function bindEvents() {
        $(document).on("click", ".js-email-one, [data-role='email-one']", function () {
            const id = $(this).data("pokemonId");
            const name = ($(this).data("pokemonName") || "").trim();
            openModal("single", { id, name });
        });

        $(document).on("click", ".js-email-all, [data-role='email-all']", function () {
            openModal("all");
        });

        $btnConfirm.on("click", onConfirm);

        $emailTo.on("input", function () {
            const val = $(this).val().trim();
            const ok = val.length > 0 && isValidEmail(val);
            $btnConfirm.prop("disabled", !ok);
            markInvalid($(this), !ok && val.length > 0);
        });
    }
    // #endregion


    // #region === UI COPY ===
    function applyCopy(mode, meta = {}) {
        if (mode === "single") {
            $titlePrefix.text("Enviar información de");
            $targetName.text(cap(meta.name || ""));
            const pokemonName = cap(meta.name || "");
            $desc.html(`Se enviará la ficha completa del Pokémon <span class="pokemon-highlight">${pokemonName}</span> incluyendo todos sus datos técnicos y estadísticas.`);
            $help.text("Incluye ID, nombre, especie, tipos, altura, peso, experiencia base y habilidades.");
            $btnConfirm.text("Enviar ahora");
        } else {
            $titlePrefix.text("Enviar información del listado actual");
            $targetName.text("");
            $desc.text("Se enviará la información de todos los Pokémon visibles en la página actual.");
            $help.text("Se respetarán los filtros y la paginación vigentes");
            $btnConfirm.text("Continuar");
        }

        $btnConfirm.prop("disabled", true);
        markInvalid($emailTo, false);
    }
    // #endregion


    // #region === ACCIONES (ABRIR / CONFIRMAR / ENVIAR) ===
    function openModal(mode, meta = {}) {
        if (isBusy) return;
        $mode.val(mode);
        $singleId.val(meta.id || "");
        $emailTo.val("");
        applyCopy(mode, meta);
        modal.show();
    }

    async function onConfirm() {
        if (isBusy) return;

        try {
            const mode = $mode.val();
            const to = ($emailTo.val() || "").trim();

            if (!to || !isValidEmail(to)) {
                markInvalid($emailTo, true);
                shake($emailTo);
                return;
            }

            if (mode === "single") {
                await sendOne($singleId.val(), to);
            } else {
                await sendAll(to);
            }
        } catch (err) {
            console.error(err);
            toastErr("Ocurrió un error al enviar el correo.");
        }
    }

    function setBusy(busy) {
        isBusy = busy;
        const baseText = ($mode.val() === "single" ? "Enviar ahora" : "Continuar");
        const ok = !$emailTo.prop("disabled") && isValidEmail(($emailTo.val() || "").trim());
        $btnConfirm.prop("disabled", busy || !ok)
            .text(busy ? "Enviando..." : baseText);
        $emailTo.prop("disabled", busy);
    }

    function sendOne(id, toEmail) {
        const token = $form.find('input[name="__RequestVerificationToken"]').val();
        setBusy(true);

        return $.ajax({
            url: urls.sendOne,
            method: "POST",
            headers: { "RequestVerificationToken": token },
            data: { id, toEmail }
        })
            .done(() => {
                toastOk("¡Correo enviado!");
                modal.hide();
            })
            .fail((xhr) => {
                console.error(xhr);
                toastErr("Ocurrió un error al enviar el correo.");
            })
            .always(() => setBusy(false));
    }

    async function sendAll(toEmail) {
        const token = $form.find('input[name="__RequestVerificationToken"]').val();
        const fd = new FormData($filters[0]);
        fd.append("toEmail", toEmail);

        setBusy(true);
        try {
            const resp = await fetch(urls.sendAll, {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body: fd
            });
            if (!resp.ok) throw new Error(await safeText(resp));
            toastOk((await safeText(resp)) || "Correo enviado con la lista actual.");
            modal.hide();
        } catch (err) {
            console.error(err);
            toastErr("Ocurrió un error al enviar el correo.");
        } finally {
            setBusy(false);
        }
    }
    // #endregion


    // #region === HELPERS ===
    async function safeText(r) { try { return await r.text(); } catch { return ""; } }

    function isValidEmail(s) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/i;
        return re.test(s);
    }

    function markInvalid($el, invalid) {
        $el.toggleClass("is-invalid", !!invalid)
            .attr("aria-invalid", invalid ? "true" : "false");
    }

    function cap(s) { return (s || "").toString().toLowerCase().replace(/^./, m => m.toUpperCase()); }

    function shake($el) { $el.addClass("is-invalid"); setTimeout(() => $el.removeClass("is-invalid"), 1200); }

    function toastOk(msg) { toast(msg, "success"); }
    function toastErr(msg) { toast(msg, "danger"); }
    function toastInfo(msg) { toast(msg, "info"); }

    function toast(msg, type) {
        try {
            if (window.UI?.toast) window.UI.toast(type, msg);
            else alert(msg);
        } catch (err) {
            console.error(err);
        }
    }
    // #endregion


    // #region === EXPORTAR MÓDULO ===
    return { init };
    // #endregion

})();
