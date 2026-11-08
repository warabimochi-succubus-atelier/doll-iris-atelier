// Browser helpers for Doll Iris Atelier (loaded as a JS module from C#).
// Everything stays on this device: IndexedDB for the favorites, and files the user saves or shares.

const DB_NAME = "doll-iris-atelier";
const STORE = "kv";

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = () => req.result.createObjectStore(STORE);
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

/** Text stored under key, or null. */
export async function load(key) {
    const db = await openDb();
    try {
        return await new Promise((resolve, reject) => {
            const req = db.transaction(STORE, "readonly").objectStore(STORE).get(key);
            req.onsuccess = () => resolve(typeof req.result === "string" ? req.result : null);
            req.onerror = () => reject(req.error);
        });
    } finally {
        db.close();
    }
}

/** Stores text under key; resolves once the write is committed. */
export async function save(key, text) {
    const db = await openDb();
    try {
        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, "readwrite");
            tx.objectStore(STORE).put(text, key);
            tx.oncomplete = () => resolve();
            tx.onerror = () => reject(tx.error);
            tx.onabort = () => reject(tx.error);
        });
    } finally {
        db.close();
    }
}

/** Asks the browser not to clear this site's storage under pressure. True when granted. */
export async function requestPersistence() {
    try {
        if (navigator.storage && navigator.storage.persist) {
            return (await navigator.storage.persisted()) || (await navigator.storage.persist());
        }
    } catch { /* not supported */ }
    return false;
}

/** Saves text as a file (download). */
export function download(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

/** True when the share sheet can take files (phones / tablets). */
export function canShareFiles() {
    try {
        const probe = new File(["{}"], "probe.json", { type: "application/json" });
        return !!(navigator.canShare && navigator.canShare({ files: [probe] }));
    } catch {
        return false;
    }
}

/** Opens the share sheet with a file. Resolves to false when the user cancelled. */
export async function shareFile(fileName, text, mimeType) {
    const file = new File([text], fileName, { type: mimeType });
    try {
        await navigator.share({ files: [file] });
        return true;
    } catch (e) {
        if (e && e.name === "AbortError") return false;
        throw e;
    }
}

export function confirmDialog(message) {
    return window.confirm(message);
}
