window.PokemonDetails = (function () {
    let $btn, $form;

    function init() {
        $form = document.getElementById('emailForm');
        $btn = $form?.querySelector('button[type="button"]');

        if (!$btn || !$form) return;

        $btn.addEventListener('click', onSend);
    }

    async function onSend() {
        $btn.disabled = true;

        try {
            const token = $form.querySelector('input[name="__RequestVerificationToken"]').value;
            const res = await fetch($form.action, {
                method: 'POST',
                headers: { 'RequestVerificationToken': token }
            });

            if (!res.ok) throw new Error();

            const text = await res.text();

            UI.toast('success', text || 'Correo enviado correctamente');
        } catch {
            UI.toast('danger', 'No se pudo enviar el correo');
        } finally {
            $btn.disabled = false;
        }
    }

    return { init };
})();
