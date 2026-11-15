using Microsoft.JSInterop;

namespace DollIrisGenerator.Web.Services;

/// <summary>The browser features the app uses (wwwroot/js/atelier.js): IndexedDB, file save, modal dialogs.</summary>
public sealed class BrowserApi : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public BrowserApi(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/atelier.js").AsTask());
    }

    private Task<IJSObjectReference> Module => _module.Value;

    public async Task<string?> LoadAsync(string key) => await (await Module).InvokeAsync<string?>("load", key);

    public async Task SaveAsync(string key, string text) => await (await Module).InvokeVoidAsync("save", key, text);

    public async Task<bool> RequestPersistenceAsync() => await (await Module).InvokeAsync<bool>("requestPersistence");

    public async Task DownloadAsync(string fileName, string text, string mimeType) =>
        await (await Module).InvokeVoidAsync("download", fileName, text, mimeType);

    /// <summary>
    /// Opens the &lt;dialog&gt; with this id as a modal and waits until it closes: true when it was closed by a
    /// button with value="ok" (ConfirmDialog), false for any other way (キャンセル, Esc, a tap outside).
    /// </summary>
    public async Task<bool> AskDialogAsync(string id) => await (await Module).InvokeAsync<bool>("askDialog", id);

    /// <summary>Opens the &lt;dialog&gt; with this id as a modal.</summary>
    public async Task ShowDialogAsync(string id) => await (await Module).InvokeVoidAsync("showDialog", id);

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try { await (await _module.Value).DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
