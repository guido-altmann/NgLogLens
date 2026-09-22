using System.Text;

namespace LogLens.Core.Parsing;

/// <summary>
/// Eine einzulesende Logquelle. Core kennt keine Dateien und keinen Browser, nur einen
/// <see cref="TextReader"/>, der bei Bedarf geöffnet wird.
/// </summary>
public sealed class LogFileSource(string name, Func<CancellationToken, ValueTask<TextReader>> openAsync)
{
    public string Name { get; } = name;

    public ValueTask<TextReader> OpenAsync(CancellationToken cancellationToken = default)
        => openAsync(cancellationToken);

    public static LogFileSource FromText(string name, string text)
        => new(name, _ => ValueTask.FromResult<TextReader>(new StringReader(text)));

    public static LogFileSource FromReader(string name, TextReader reader)
        => new(name, _ => ValueTask.FromResult(reader));

    /// <summary>
    /// Für den Upload im Browser: der Stream wird erst beim Lesen geöffnet und nie
    /// vollständig in den Speicher geholt.
    /// </summary>
    /// <param name="bufferSize">Lesepuffer; im Browser ist jeder Zugriff ein Interop-Aufruf.</param>
    public static LogFileSource FromStream(
        string name,
        Func<CancellationToken, ValueTask<Stream>> openStreamAsync,
        int bufferSize)
        => new(name, async cancellationToken =>
        {
            var stream = await openStreamAsync(cancellationToken).ConfigureAwait(false);
            return new StreamReader(
                stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize);
        });
}
