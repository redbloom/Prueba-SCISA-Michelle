(function () {
    const ROOT_ID = "toast-root";
    const TYPE_TO_CLASS = {
        success: "pokemon-toast-success",
        danger: "pokemon-toast-danger",
        warning: "pokemon-toast-warning",
        info: "pokemon-toast-info"
    };

    function ensureRoot() {
        let root = document.getElementById(ROOT_ID);
        if (!root) {
            root = document.createElement("div");
            root.id = ROOT_ID;
            root.className = "position-fixed top-0 start-50 translate-middle-x p-3";
            root.style.zIndex = "1080";
            document.body.appendChild(root);
        }
        return root;
    }

    function toast(type, message, opts = {}) {
        const root = ensureRoot();
        const toastClass = TYPE_TO_CLASS[type] || TYPE_TO_CLASS.info;
        const id = "t" + Date.now() + Math.floor(Math.random() * 1000);

        const html = `
      <div id="${id}" class="toast pokemon-toast ${toastClass}"
           role="status" aria-live="polite" aria-atomic="true"
           data-bs-delay="${opts.delay ?? 3000}">
        <div class="d-flex justify-content-center align-items-center">
          <div class="toast-body fw-semibold">${escapeHtml(message || "")}</div>
        </div>
      </div>`;

        root.insertAdjacentHTML("beforeend", html);
        const el = document.getElementById(id);

        const t = bootstrap.Toast.getOrCreateInstance(el);
        t.show();

        el.addEventListener("hidden.bs.toast", () => el.remove());
    }

    function escapeHtml(s) {
        return (s || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // Helpers rápidos
    window.UI = window.UI || {};
    window.UI.toast = toast;
    window.UI.toastSuccess = (msg, opts) => toast('success', msg, opts);
    window.UI.toastError = (msg, opts) => toast('danger', msg, opts);
    window.UI.toastWarning = (msg, opts) => toast('warning', msg, opts);
    window.UI.toastInfo = (msg, opts) => toast('info', msg, opts);
})();