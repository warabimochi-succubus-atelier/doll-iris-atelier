// Preview images (IrisPreview): the SVG text becomes a blob: URL for <img>, so .NET does not have to
// encode a large Base64 data URI on every redraw (slow in WebAssembly). Called through [JSImport].

/** A blob: URL showing the given SVG text. Release it with revoke() once it is no longer shown. */
export function svgUrl(svg) {
    return URL.createObjectURL(new Blob([svg], { type: "image/svg+xml" }));
}

export function revoke(url) {
    URL.revokeObjectURL(url);
}
