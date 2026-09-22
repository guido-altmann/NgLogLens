using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

/// <summary>
/// Stufen „Parse" und „Dedupe" der Pipeline. Liest zeilenweise, gibt den UI-Thread
/// regelmäßig frei und meldet Fortschritt (CLAUDE.md, WASM-Fallstricke).
/// </summary>
public sealed class LogParseService(
    LogLineParserChain chain,
    ErrorLineDeduplicator deduplicator,
    ParserOptions options)
{
    public Task<ParseResult> ParseAsync(
        LogFileSource file,
        IProgress<ParseProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => ParseAsync([file], progress, cancellationToken);

    public async Task<ParseResult> ParseAsync(
        IReadOnlyList<LogFileSource> files,
        IProgress<ParseProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var accessEntries = new List<AccessEntry>();
        var errorEntries = new List<ErrorEntry>();
        var unknownSamples = new List<UnknownLineSample>();
        var fileInfos = new List<SourceFileInfo>(files.Count);

        // Identische Rohzeilen werden nur über Dateigrenzen hinweg entfernt: innerhalb
        // einer Datei können zwei zeichengleiche Zeilen zwei echte Anfragen sein.
        var linesFromPreviousFiles = files.Count > 1
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;

        var counters = new Counters();
        var lineNumber = 0;

        for (var fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[fileIndex];
            var firstLineNumber = lineNumber + 1;
            var linesInFile = 0;
            var linesOfThisFile = linesFromPreviousFiles is null
                ? null
                : new HashSet<string>(StringComparer.Ordinal);

            var reader = await file.OpenAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
                {
                    lineNumber++;
                    linesInFile++;
                    counters.TotalLines++;

                    if (counters.TotalLines % options.YieldInterval == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(new ParseProgress(file.Name, fileIndex, files.Count, counters.TotalLines));
                        await Task.Yield();
                    }

                    linesOfThisFile?.Add(line);

                    if (linesFromPreviousFiles?.Contains(line) == true)
                    {
                        counters.IdenticalLinesRemoved++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        counters.BlankLines++;
                        continue;
                    }

                    Consume(line, lineNumber, file.Name, counters, accessEntries, errorEntries, unknownSamples);
                }
            }
            finally
            {
                reader.Dispose();
            }

            if (linesOfThisFile is not null)
            {
                linesFromPreviousFiles!.UnionWith(linesOfThisFile);
            }

            progress?.Report(new ParseProgress(file.Name, fileIndex, files.Count, counters.TotalLines));
            fileInfos.Add(new SourceFileInfo(fileIndex, file.Name, firstLineNumber, lineNumber, linesInFile));
        }

        var dedupe = deduplicator.Dedupe(errorEntries);

        var diagnostics = new ParseDiagnostics(
            TotalLines: counters.TotalLines,
            AccessLines: counters.AccessLines,
            TruncatedAccessLines: counters.TruncatedAccessLines,
            ErrorLines: counters.ErrorLines,
            TruncatedErrorLines: counters.TruncatedErrorLines,
            NotFoundPageErrorLines: dedupe.NotFoundPageErrorLines,
            IdenticalLinesRemoved: counters.IdenticalLinesRemoved,
            BlankLines: counters.BlankLines,
            UnknownLines: counters.UnknownLines,
            LinesWithoutClientIpHeader: counters.LinesWithoutClientIpHeader,
            UnknownSamples: unknownSamples,
            Files: fileInfos);

        return new ParseResult(accessEntries, dedupe.Kept, diagnostics);
    }

    private void Consume(
        string line,
        int lineNumber,
        string fileName,
        Counters counters,
        List<AccessEntry> accessEntries,
        List<ErrorEntry> errorEntries,
        List<UnknownLineSample> unknownSamples)
    {
        switch (chain.Parse(line, lineNumber))
        {
            case ParsedAccessLine access:
                accessEntries.Add(access.Entry);
                if (access.Entry.IsTruncated)
                {
                    counters.TruncatedAccessLines++;
                }
                else
                {
                    counters.AccessLines++;
                }

                if (!access.Entry.HasClientIpHeader)
                {
                    counters.LinesWithoutClientIpHeader++;
                }

                break;

            case ParsedErrorLine error:
                errorEntries.Add(error.Entry);
                if (error.Entry.IsTruncated)
                {
                    counters.TruncatedErrorLines++;
                }
                else
                {
                    counters.ErrorLines++;
                }

                break;

            default:
                counters.UnknownLines++;
                if (unknownSamples.Count < options.MaxUnknownSamples)
                {
                    unknownSamples.Add(new UnknownLineSample(lineNumber, fileName, Shorten(line)));
                }

                break;
        }
    }

    private string Shorten(string line) =>
        line.Length <= options.UnknownSampleLength ? line : line[..options.UnknownSampleLength];

    private sealed class Counters
    {
        public int TotalLines;
        public int AccessLines;
        public int TruncatedAccessLines;
        public int ErrorLines;
        public int TruncatedErrorLines;
        public int IdenticalLinesRemoved;
        public int BlankLines;
        public int UnknownLines;
        public int LinesWithoutClientIpHeader;
    }
}
