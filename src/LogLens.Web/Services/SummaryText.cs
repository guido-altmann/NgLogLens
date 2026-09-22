using System.Globalization;
using LogLens.Core.Models;

namespace LogLens.Web.Services;

/// <summary>
/// Die Kernaussage der Auswertung als Satz (SPEC 8.2). Steht an einer Stelle, damit
/// Upload-Seite und Übersicht dasselbe sagen.
/// </summary>
public static class SummaryText
{
    /// <summary>„Von 29 Anfragen waren 7 Seitenaufrufe von Menschen."</summary>
    public static string Headline(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsEmpty)
        {
            return "In dieser Datei steht keine einzige Anfrage.";
        }

        var requests = result.Requests == 1
            ? "einer Anfrage"
            : $"{Number(result.Requests)} Anfragen";

        var pageViews = result.PageViews switch
        {
            0 => "war kein einziger Seitenaufruf von Menschen",
            1 => "war 1 Seitenaufruf von Menschen",
            _ => $"waren {Number(result.PageViews)} Seitenaufrufe von Menschen",
        };

        return $"Von {requests} {pageViews}.";
    }

    /// <summary>„13 Anfragen (44,8 %) waren Angriffe." – null, wenn es keine gab.</summary>
    public static string? Attacks(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var attacks = result.Count(TrafficClass.Attack);
        if (attacks == 0)
        {
            return null;
        }

        var share = result.Classes.Single(c => c.Class == TrafficClass.Attack).Share
            .ToString("P1", CultureInfo.CurrentCulture);

        return attacks == 1
            ? $"1 Anfrage ({share}) war ein Angriff."
            : $"{Number(attacks)} Anfragen ({share}) waren Angriffe.";
    }

    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
