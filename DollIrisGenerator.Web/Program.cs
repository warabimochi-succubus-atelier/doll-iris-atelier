using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DollIrisGenerator.Web;
using DollIrisGenerator.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One page, one user: the design being edited and the favorites live for the whole session
builder.Services.AddSingleton<BrowserApi>();
builder.Services.AddSingleton<DesignSession>();
builder.Services.AddSingleton<FavoritesRepository>();

// The preview images are handed to the browser through this module (PreviewInterop)
if (OperatingSystem.IsBrowser()) await JSHost.ImportAsync(PreviewInterop.ModuleName, PreviewInterop.ModulePath);

await builder.Build().RunAsync();
