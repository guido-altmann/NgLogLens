using Microsoft.JSInterop;

namespace LogLens.Web.Services;

/// <summary>
/// Download und Zwischenablage über ein kleines lokales JS-Modul. Die Datei entsteht
/// als Blob im Browser; nichts verlässt den Rechner (CLAUDE.md, Datenschutz).
/// </summary>
public sealed class FileDownloadService(IJSRuntime js) : IAsyncDisposable
{
    public const string ModulePath = "./js/loglens.js";

    private IJSObjectReference? _module;

    public async Task DownloadTextAsync(
        string fileName, string content, string contentType, CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("downloadText", cancellationToken, fileName, content, contentType);
    }

    /// <returns>False, wenn der Browser den Zugriff auf die Zwischenablage verweigert.</returns>
    public async Task<bool> CopyTextAsync(string content, CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        return await module.InvokeAsync<bool>("copyText", cancellationToken, content);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Seite wird gerade geschlossen; das Modul ist ohnehin weg.
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
}
