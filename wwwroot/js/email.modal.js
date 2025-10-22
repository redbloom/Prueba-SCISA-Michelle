// wwwroot/js/email.modal.js
window.PokemonEmail = (function () {
    let $modal, modal, $form, $filters,
        $titlePrefix, $targetName, $desc, $help,
        $emailTo, $mode, $singleId, $btnConfirm;

    // Ajusta aquí tu endpoint real si no lo inyectas por data-attrs
    const urls = {
        sendOne: "/Pokemon/SendToEmail" // Controller.Action actual
        // sendAll quedará pendiente a propósito
    };

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

        modal = bootstrap.Modal.getOrCreateInstance($modal[0]);

        // Abrir modal (INDIVIDUAL)
        $(document).on("click", ".js-email-one, [data-role='email-one']", function () {
            const id = $(this).data("pokemonId");
            const name = ($(this).data("pokemonName") || "").trim();
            openModal("single", { id, name });
        });


        // Abrir modal (LISTADO)
        $(document).on("click", ".js-email-all, [data-role='email-all']", function () {
            openModal("all");
        });

        // Confirmar
        $btnConfirm.on("click", onConfirm);
    }

    // UI COPY por modo
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
    }

    function openModal(mode, meta = {}) {
        $mode.val(mode);
        $singleId.val(meta.id || "");
        $emailTo.val("");
        applyCopy(mode, meta);
        modal.show();
    }

    async function onConfirm() {
        const mode = $mode.val();
        const to = ($emailTo.val() || "").trim();
        if (!to) return shake($emailTo);

        if (mode === "single") {
            await sendOne($singleId.val(), to);
        } else {
            // Listado: pendiente en backend, solo avisamos al usuario
            toastInfo("Esta acción está lista en la vista, pero el envío del listado aún está pendiente en el backend.");
            modal.hide();
        }
    }

    // === Envío individual ===
    function sendOne(id, toEmail) {
        const token = $form.find('input[name="__RequestVerificationToken"]').val();
        $btnConfirm.prop("disabled", true).text("Enviando...");

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
                const msg = xhr?.responseText || "No se pudo enviar el correo.";
                toastErr(msg);
            })
            .always(() => {
                $btnConfirm.prop("disabled", false).text("Enviar ahora");
            });
    }

    // Helpers UI
    function cap(s) { return (s || "").toString().toLowerCase().replace(/^./, m => m.toUpperCase()); }
    function shake($el) { $el.addClass("is-invalid"); setTimeout(() => $el.removeClass("is-invalid"), 1200); }
    function toastOk(msg) { toast(msg, "success"); }
    function toastErr(msg) { toast(msg, "danger"); }
    function toastInfo(msg) { toast(msg, "info"); }
    function toast(msg, type) {
        if (window.UI?.toast) {
            window.UI.toast(type, msg);
        } else {
            alert(msg);
        }
    }

    return { init };
})();
