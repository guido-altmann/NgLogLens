using System.Globalization;
using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace LogLens.Web.Pages;

/// <summary>
/// Start- und Upload-Seite (SPEC 8.1). Liest die Datei zeilenweise im Browser,
/// meldet Fortschritt und lässt sich jederzeit abbrechen.
/// </summary>
public partial class Upload : ComponentBase, IDisposable
{
    [Inject] private LogAnalysisPipeline Pipeline { get; set; } = null!;

    [Inject] private ParserOptions ParserOptions { get; set; } = null!;

    [Inject] private AnalysisState State { get; set; } = null!;

    private MudFileUpload<IReadOnlyList<IBrowserFile>>? _fileUpload;
    private CancellationTokenSource? _cancellation;
    private AnalysisProgress? _progress;
    private IReadOnlyList<string> _fileNames = [];
    private string? _error;
    private bool _cancelled;
    private bool _dragging;

    /// <summary>Endungen im Dateidialog. Coolify exportiert .log, Browser speichern oft als .txt.</summary>
    private const string AcceptedFileTypes = ".log,.txt";

    private bool IsRunning => _cancellation is not null;

    /// <summary>Hebt die Ablagefläche hervor, solange eine Datei darüber schwebt.</summary>
    private string DropZoneClass => _dragging ? "drop-zone drop-zone-active pa-8" : "drop-zone pa-8";

    private string MaxFileSizeText =>
        $"{ParserOptions.MaxFileSizeBytes / (1024d * 1024d):N0} MB";

    private bool IsIndeterminate => _progress?.IsIndeterminate ?? true;

    private double ProgressPercent => _progress?.Percent ?? 0;

    private string StageText => _progress?.Stage switch
    {
        AnalysisStage.Reading => "Datei wird gelesen …",
        AnalysisStage.Classifying => "Anfragen werden eingeordnet …",
        AnalysisStage.Aggregating => "Kennzahlen werden berechnet …",
        AnalysisStage.Done => "Fertig.",
        _ => "Auswertung wird vorbereitet …",
    };

    private string ProgressDetail => _progress switch
    {
        { Stage: AnalysisStage.Reading } p =>
            $"{Number(p.Done)} Zeilen gelesen{(p.FileName is null ? string.Empty : $" – {p.FileName}")}",
        { Stage: AnalysisStage.Classifying } p =>
            $"{Number(p.Done)} von {Number(p.Total)} Anfragen eingeordnet",
        _ => string.Empty,
    };

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private string FileSummary => _fileNames.Count switch
    {
        0 => "Auswertung",
        1 => _fileNames[0],
        _ => $"{_fileNames.Count} Dateien: {string.Join(", ", _fileNames)}",
    };

    /// <summary>Kernaussage der Auswertung (SPEC 8.2).</summary>
    private static string Summary(AnalysisResult result) =>
        string.Join(' ', new[] { SummaryText.Headline(result), SummaryText.Attacks(result) }
            .Where(s => s is not null));

    private async Task OnFilesSelectedAsync(IReadOnlyList<IBrowserFile>? files)
    {
        if (files is null || files.Count == 0 || IsRunning)
        {
            return;
        }

        _error = null;
        _cancelled = false;
        _progress = null;
        _fileNames = [.. files.Select(f => f.Name)];
        State.Clear();

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        try
        {
            var result = await Pipeline.AnalyzeAsync(
                BrowserLogFiles.ToSources(files, ParserOptions),
                new UiProgress(ReportProgress),
                cancellation.Token);

            if (result.Diagnostics.TotalLines == 0)
            {
                _error = "Die Datei enthält keine Zeilen.";
                State.Clear();
            }
            else
            {
                State.Set(result);
            }
        }
        catch (OperationCanceledException)
        {
            _cancelled = true;
            State.Clear();
        }
        catch (Exception exception)
        {
            // Der häufigste Fall ist die Größenbegrenzung von OpenReadStream.
            _error = $"Die Datei konnte nicht gelesen werden: {exception.Message}";
            State.Clear();
        }
        finally
        {
            _cancellation = null;
            cancellation.Dispose();
            _progress = null;

            // Damit dieselbe Datei erneut geöffnet werden kann: der Browser meldet
            // sonst keine Änderung, wenn zweimal dieselbe Auswahl getroffen wird.
            if (_fileUpload is not null)
            {
                await _fileUpload.ClearAsync();
            }
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private void ReportProgress(AnalysisProgress value)
    {
        _progress = value;

        // Die Pipeline meldet aus ihrem eigenen Ablauf; das Neuzeichnen gehört
        // in jedem Fall auf den Renderer-Thread.
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>Reicht Fortschritt unverändert an die Komponente weiter.</summary>
    private sealed class UiProgress(Action<AnalysisProgress> report) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => report(value);
    }
}
