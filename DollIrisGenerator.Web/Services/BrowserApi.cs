using Microsoft.JSInterop;

namespace DollIrisGenerator.Web.Services;

/// <summary>The browser features the app uses (wwwroot/js/atelier.js): IndexedDB, file save / share, confirm.</summary>
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

    public async Task<bool> CanShareFilesAsync() => await (await Module).InvokeAsync<bool>("canShareFiles");

    /// <returns>False when the user closed the share sheet without sharing.</returns>
    public async Task<bool> ShareFileAsync(string fileName, string text, string mimeType) =>
        await (await Module).InvokeAsync<bool>("shareFile", fileName, text, mimeType);

    public async Task<bool> ConfirmAsync(string message) => await (await Module).InvokeAsync<bool>("confirmDialog", message);

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try { await (await _module.Value).DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
