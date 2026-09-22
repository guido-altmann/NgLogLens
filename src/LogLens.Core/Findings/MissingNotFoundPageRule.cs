using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Fehlende 404-Seite": nginx sucht bei jedem 404 die Fehlerseite und schreibt,
/// wenn sie fehlt, eine zweite Zeile ins Log. Diese Folgefehler zählt die Dedupe-Stufe
/// (SPEC 3). Mittlere Priorität: das Log wird unübersichtlich, gefährlich ist es nicht.
/// </summary>
public sealed class MissingNotFoundPageRule(ParserOptions parserOptions) : IFindingRule
{
    public string Id => "missing-404-page";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = result.Diagnostics.NotFoundPageErrorLines;
        if (lines == 0)
        {
            return null;
        }

        var page = parserOptions.NotFoundPageFileName;

        return new Finding(
            Id,
            FindingPriority.Medium,
            "Fehlende 404-Seite",
            $"{FindingText.Number(lines)} Error-Zeilen melden, dass „{page}“ nicht existiert. "
            + "Jeder 404 erzeugt dadurch eine zweite Zeile im Log.",
            $"Entweder eine eigene Fehlerseite ausliefern oder das Protokollieren fehlender Dateien abschalten.")
        {
            Snippet = new FindingSnippet("nginx-Konfiguration", "nginx",
                $$"""
                  # Variante 1: eigene Fehlerseite
                  error_page 404 /{{page}};
                  location = /{{page}} {
                      internal;
                  }

                  # Variante 2: keine Fehlerseite, dafür Ruhe im Log
                  log_not_found off;
                  """),
        };
    }
}
