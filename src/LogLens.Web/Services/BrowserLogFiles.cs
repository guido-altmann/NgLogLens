using LogLens.Core;
using LogLens.Core.Parsing;
using Microsoft.AspNetCore.Components.Forms;

namespace LogLens.Web.Services;

/// <summary>
/// Brücke vom Browser-Upload zur Core-Pipeline. Core kennt keine Dateien, nur einen
/// <see cref="TextReader"/>; hier entsteht er aus einem <see cref="IBrowserFile"/>.
/// </summary>
public static class BrowserLogFiles
{
    /// <summary>
    /// <see cref="IBrowserFile.OpenReadStream"/> hat ein Standardlimit von 500 KB.
    /// Hier gilt das konfigurierte Limit (CLAUDE.md, WASM-Fallstricke).
    /// </summary>
    public static LogFileSource ToSource(IBrowserFile file, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);

        return LogFileSource.FromStream(
            file.Name,
            cancellationToken => ValueTask.FromResult(
                file.OpenReadStream(options.MaxFileSizeBytes, cancellationToken)),
            options.ReadBufferSizeBytes);
    }

    public static IReadOnlyList<LogFileSource> ToSources(
        IReadOnlyList<IBrowserFile> files, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(files);

        return [.. files.Select(file => ToSource(file, options))];
    }
}
