using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Web.Pages;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Web.Tests;

/// <summary>
/// Upload-Seite (SPEC 8.1). Die Datei wird im Test wie im Browser über
/// <see cref="InputFile"/> hereingereicht; gerechnet wird mit der echten Pipeline.
/// </summary>
public sealed class UploadTests : MudBlazorTestContext
{
    /// <summary>
    /// Zwei Access-Zeilen: ein Angriff (404 auf <c>.env</c>) und ein Seitenaufruf.
    /// IPs aus den Dokumentationsbereichen (RFC 5737), Domain example.de.
    /// </summary>
    private const string SampleLog = """
        10.0.1.9 - - [27/Aug/2026:10:16:59 +0000] "GET /.env HTTP/1.1" 404 555 "-" "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" "203.0.113.10"
        10.0.1.9 - - [28/Aug/2026:08:15:44 +0000] "GET / HTTP/1.1" 200 8123 "https://example.de/" "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15" "198.51.100.23"
        """;

    /// <summary>
    /// Wartezeit für Zwischenstände. Großzügig, weil parallel laufende Testprojekte
    /// die Standardsekunde von bUnit auf langsamen Rechnern reißen können.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly AnalysisState _state = new();

    public UploadTests()
    {
        Services.AddLogLensCore();
        Services.AddSingleton(_state);
    }

    [Fact]
    public void Zeigt_den_Datenschutzhinweis_und_die_Ablageflaeche()
    {
        var cut = Render<Upload>();

        Assert.Contains(
            "Die Datei verlässt deinen Browser nicht",
            cut.Find("[data-testid=privacy-hint]").TextContent);
        Assert.NotNull(cut.Find("[data-testid=drop-zone]"));
        Assert.Contains("Datei auswählen", cut.Find("[data-testid=pick-file]").TextContent);
    }

    /// <summary>
    /// Regression: Das Dateifeld war per <c>hidden</c> ausgeblendet. Auf ihm sitzen
    /// aber die Drag-Handler von MudFileUpload, und ein display:none-Element bekommt
    /// keine Drag-Events – Drag &amp; Drop war damit tot, nur der Knopf ging.
    /// </summary>
    [Fact]
    public void Das_Dateifeld_liegt_sichtbar_als_Overlay_ueber_der_Ablageflaeche()
    {
        var cut = Render<Upload>();

        var input = cut.Find("input[type=file]");

        Assert.False(input.HasAttribute("hidden"));
        Assert.Contains("drop-zone-input", input.ClassName ?? string.Empty);
        Assert.Contains("drop-zone-host", cut.Find(".mud-file-upload").ClassName ?? string.Empty);

        // Der sichtbare Knopf bleibt die Tab-Station, nicht das unsichtbare Feld.
        Assert.Equal("-1", input.GetAttribute("tabindex"));
        Assert.Equal("Logdatei auswählen", input.GetAttribute("aria-label"));
    }

    [Fact]
    public void Eine_Datei_ueber_der_Flaeche_hebt_die_Ablageflaeche_hervor()
    {
        var cut = Render<Upload>();

        cut.Find("input[type=file]").DragEnter();
        Assert.Contains("drop-zone-active", cut.Find("[data-testid=drop-zone]").ClassName ?? string.Empty);

        cut.Find("input[type=file]").DragLeave();
        Assert.DoesNotContain("drop-zone-active", cut.Find("[data-testid=drop-zone]").ClassName ?? string.Empty);
    }

    [Fact]
    public void Waehrend_der_Auswertung_nimmt_die_Flaeche_keine_weitere_Datei_an()
    {
        Services.AddSingleton(new ParserOptions { YieldInterval = 1 });

        var gate = new TaskCompletionSource();
        var cut = Render<Upload>();

        UploadLog(cut, ManyLines(200), blockAfterFirstRead: gate.Task);
        cut.WaitForElement("[data-testid=progress]", Patience);

        Assert.True(cut.Find("input[type=file]").HasAttribute("disabled"));

        gate.SetResult();
        cut.WaitForElement("[data-testid=summary]", Patience);
    }

    [Fact]
    public void Meldet_das_konfigurierte_Groessenlimit_statt_der_500_KB_Vorgabe()
    {
        Services.AddSingleton(new ParserOptions { MaxFileSizeBytes = 50L * 1024 * 1024 });

        var cut = Render<Upload>();

        Assert.Contains("50 MB", cut.Find("[data-testid=drop-zone]").TextContent);
    }

    [Fact]
    public void Zeigt_ohne_Datei_weder_Fortschritt_noch_Zusammenfassung()
    {
        var cut = Render<Upload>();

        Assert.Empty(cut.FindAll("[data-testid=progress]"));
        Assert.Empty(cut.FindAll("[data-testid=summary]"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact]
    public void Wertet_eine_hereingereichte_Datei_aus_und_zeigt_die_Kernaussage()
    {
        var cut = Render<Upload>();

        UploadLog(cut, SampleLog);

        var summary = cut.WaitForElement("[data-testid=summary]", Patience);

        Assert.Contains("sample-nginx.log", summary.TextContent);
        Assert.Equal(
            "Von 2 Anfragen war 1 Seitenaufruf von Menschen. 1 Anfrage (50,0 %) war ein Angriff.",
            cut.Find("[data-testid=summary-sentence]").TextContent.Trim());
    }

    [Fact]
    public void Legt_das_Ergebnis_im_Zustand_ab_damit_die_Uebersicht_es_findet()
    {
        var cut = Render<Upload>();

        UploadLog(cut, SampleLog);
        cut.WaitForElement("[data-testid=summary]", Patience);

        Assert.NotNull(_state.Result);
        Assert.Equal(2, _state.Result.Requests);
        Assert.Equal(1, _state.Result.Count(TrafficClass.Human));
        Assert.Equal(1, _state.Result.Count(TrafficClass.Attack));
        Assert.Equal(1, _state.Result.PageViews);
        Assert.Equal(["sample-nginx.log"], _state.Result.FileNames);
    }

    [Fact]
    public void Verlinkt_nach_der_Auswertung_auf_die_Uebersicht()
    {
        var cut = Render<Upload>();

        UploadLog(cut, SampleLog);
        cut.WaitForElement("[data-testid=summary]", Patience);

        Assert.Equal("/overview", cut.Find("[data-testid=to-overview]").GetAttribute("href"));
    }

    [Fact]
    public void Zeigt_Fortschritt_mit_Zeilenzahl_und_Abbruch_waehrend_des_Lesens()
    {
        // YieldInterval 1: nach jeder Zeile wird gemeldet und der Thread freigegeben,
        // damit der Test den Zwischenstand überhaupt zu sehen bekommt.
        Services.AddSingleton(new ParserOptions { YieldInterval = 1 });

        var gate = new TaskCompletionSource();
        var cut = Render<Upload>();

        UploadLog(cut, ManyLines(200), blockAfterFirstRead: gate.Task);

        var progress = cut.WaitForElement("[data-testid=progress]", Patience);

        Assert.Contains("Datei wird gelesen", progress.TextContent);
        Assert.Contains("Zeilen gelesen", cut.Find("[data-testid=progress-detail]").TextContent);
        Assert.NotNull(cut.Find("[data-testid=cancel]"));

        gate.SetResult();
        cut.WaitForElement("[data-testid=summary]", Patience);
    }

    [Fact]
    public void Abbrechen_verwirft_die_Auswertung_und_speichert_nichts()
    {
        Services.AddSingleton(new ParserOptions { YieldInterval = 1 });

        var gate = new TaskCompletionSource();
        var cut = Render<Upload>();

        UploadLog(cut, ManyLines(200), blockAfterFirstRead: gate.Task);
        cut.WaitForElement("[data-testid=cancel]", Patience).Click();
        gate.SetResult();

        var cancelled = cut.WaitForElement("[data-testid=cancelled]", Patience);

        Assert.Contains("abgebrochen", cancelled.TextContent);
        Assert.Null(_state.Result);
        Assert.Empty(cut.FindAll("[data-testid=summary]"));
        Assert.Empty(cut.FindAll("[data-testid=progress]"));
    }

    [Fact]
    public void Meldet_eine_leere_Datei_als_Fehler()
    {
        var cut = Render<Upload>();

        UploadLog(cut, string.Empty);

        Assert.Contains("keine Zeilen", cut.WaitForElement("[data-testid=error]", Patience).TextContent);
    }

    [Fact]
    public void Zaehlt_nicht_erkannte_Zeilen_statt_sie_zu_verwerfen()
    {
        var cut = Render<Upload>();

        UploadLog(cut, $"{SampleLog}\nkein Logformat, nur Text");

        cut.WaitForElement("[data-testid=summary]", Patience);

        Assert.Equal(1, _state.Result!.Diagnostics.UnknownLines);
        Assert.Equal(3, _state.Result.Diagnostics.TotalLines);
    }

    private static string ManyLines(int count) =>
        string.Join('\n', Enumerable.Range(0, count).Select(i =>
            $"""10.0.1.9 - - [27/Aug/2026:10:{i / 60 % 60:00}:{i % 60:00} +0000] "GET /seite-{i}.html HTTP/1.1" 200 8123 "-" "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15" "198.51.100.23" """));

    /// <summary>
    /// Reicht Text als Datei herein, so wie der Browser es täte. <paramref name="blockAfterFirstRead"/>
    /// hält den Stream nach dem ersten Lesezugriff an, damit der Fortschritt prüfbar wird.
    /// </summary>
    private static void UploadLog(
        IRenderedComponent<Upload> cut, string content, Task? blockAfterFirstRead = null)
    {
        var file = new TestBrowserFile("sample-nginx.log", content, blockAfterFirstRead);

        cut.InvokeAsync(() => cut.FindComponent<InputFile>().Instance
            .OnChange.InvokeAsync(new InputFileChangeEventArgs([file])));
    }

    /// <summary>Ein <see cref="IBrowserFile"/> über einem Text im Speicher.</summary>
    private sealed class TestBrowserFile(string name, string content, Task? gate) : IBrowserFile
    {
        private readonly byte[] _bytes = System.Text.Encoding.UTF8.GetBytes(content);

        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;

        public long Size => _bytes.Length;

        public string ContentType => "text/plain";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException(
                    $"Die Datei ist größer als das erlaubte Limit von {maxAllowedSize} Bytes.");
            }

            return new GatedStream(_bytes, gate);
        }
    }

    /// <summary>
    /// Speicherstream, der beim zweiten Lesezugriff auf ein Signal wartet. Bildet nach,
    /// dass das Lesen im Browser asynchron und in Häppchen passiert.
    /// </summary>
    private sealed class GatedStream(byte[] bytes, Task? gate) : MemoryStream(bytes, writable: false)
    {
        private bool _gateUsed;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (gate is not null && !_gateUsed && Position > 0)
            {
                _gateUsed = true;
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}
