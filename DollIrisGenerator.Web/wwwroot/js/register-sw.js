// Registers the Service Worker (offline support). A file of its own rather than an inline <script>, so the
// Content-Security-Policy in index.html can refuse every inline script.
//
// A new version installs in the background but only takes over once every window of the app is closed, which an
// app opened from the home screen rarely is. So when one is waiting, a banner offers to switch now: the waiting
// worker is told to take over (SKIP_WAITING), and the page reloads once it has.
if ("serviceWorker" in navigator) {
    let reloading = false;
    navigator.serviceWorker.addEventListener("controllerchange", () => {
        if (reloading) return;
        reloading = true;
        location.reload();
    });

    navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" }).then(registration => {
        // Only an update: on the very first install there is no older version on screen
        const offer = worker => {
            if (worker && navigator.serviceWorker.controller) showUpdateBanner(worker);
        };
        offer(registration.waiting);
        registration.addEventListener("updatefound", () => {
            const worker = registration.installing;
            if (!worker) return;
            worker.addEventListener("statechange", () => {
                if (worker.state === "installed") offer(worker);
            });
        });

        // An app left open (home screen) is seldom reloaded: look for a new version whenever it comes back
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible") registration.update().catch(() => { /* offline */ });
        });
    });
}

function showUpdateBanner(worker) {
    if (document.querySelector(".update-banner")) return;

    const banner = document.createElement("div");
    banner.className = "update-banner";
    banner.setAttribute("role", "status");

    const text = document.createElement("p");
    text.textContent = "新しいバージョンがあります。";

    const buttons = document.createElement("div");
    buttons.className = "update-banner-buttons";

    const later = document.createElement("button");
    later.type = "button";
    later.textContent = "あとで";
    later.addEventListener("click", () => banner.remove());

    const update = document.createElement("button");
    update.type = "button";
    update.className = "primary";
    update.textContent = "更新する";
    update.addEventListener("click", () => {
        update.disabled = true;
        worker.postMessage({ type: "SKIP_WAITING" });
    });

    buttons.append(later, update);
    banner.append(text, buttons);
    document.body.append(banner);
}
