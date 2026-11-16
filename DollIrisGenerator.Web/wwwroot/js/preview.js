// Preview images (IrisPreview): the SVG text becomes a blob: URL for <img>, so .NET does not have to
// encode a large Base64 data URI on every redraw (slow in WebAssembly). Called through [JSImport].
//
// The picture is loaded off-screen first and only then handed to the page. A heavy design (many glitter
// grains, nebula, fiber noise) can take longer to load than the next redraw takes to arrive; swapping the
// <img> source before the new picture is ready leaves the preview empty until the edits stop.

/** Off-screen images, kept until their URL is released, so the loaded picture stays at hand for the page. */
const loaded = new Map();

/**
 * A blob: URL showing the given SVG text, resolved once the picture has loaded (and decoded where the
 * browser can), so putting it on an <img> shows it at once. Release it with revoke() once it is no longer shown.
 */
export function loadSvg(svg) {
    const url = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml" }));
    const img = new Image();
    loaded.set(url, img);
    return new Promise((resolve, reject) => {
        img.onload = () => {
            // Some browsers cannot decode() an SVG ahead of time; the load alone is enough there
            img.decode().catch(() => { }).then(() => resolve(url));
        };
        img.onerror = () => {
            revoke(url);
            reject(new Error("画像を読み込めませんでした"));
        };
        img.src = url;
    });
}

export function revoke(url) {
    loaded.delete(url);
    URL.revokeObjectURL(url);
}
