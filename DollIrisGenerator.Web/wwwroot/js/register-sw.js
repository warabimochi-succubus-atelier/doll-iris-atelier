// Registers the Service Worker (offline support). A file of its own rather than an inline <script>, so the
// Content-Security-Policy in index.html can refuse every inline script.
if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });
}
