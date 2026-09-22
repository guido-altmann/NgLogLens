using LogLens.Core.Models;

namespace LogLens.Core.Classification;

/// <summary>
/// Seitenaufrufe und Pfad-Normalisierung (SPEC 5.7). Ein Browser, der ein Asset
/// nachlädt, ist kein Seitenaufruf – aber ein Mensch.
/// </summary>
public static class PagePath
{
    /// <summary>
    /// Seitenaufruf: Mensch, Status 2xx oder 304, und der Pfad ist eine Seite.
    /// Bilder, Fonts, CSS, JS und Sitemaps zählen nicht.
    /// </summary>
    public static bool IsPageView(AccessEntry entry, TrafficClass trafficClass, ClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(options);

        return trafficClass == TrafficClass.Human
            && entry.Status is int status
            && IsSuccess(status)
            && IsPage(entry.Path, options);
    }

    /// <summary>2xx oder 304: die Seite wurde ausgeliefert oder war im Cache noch gültig.</summary>
    public static bool IsSuccess(int status) => status is (>= 200 and <= 299) or 304;

    /// <summary>
    /// Seite ist: <c>/</c>, eine <c>.html</c>-Datei, ein Pfad ohne Dateiendung oder
    /// ein Download mit konfigurierter Endung.
    /// </summary>
    public static bool IsPage(string? path, ClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            return false;
        }

        var extension = Extension(path);
        return extension.Length == 0
            || Contains(options.PageExtensions, extension)
            || Contains(options.DownloadExtensions, extension);
    }

    /// <summary>
    /// Normalisierung für Seitenstatistiken (SPEC 5.7): Query weg, <c>.html</c> weg,
    /// <c>/index</c> wird <c>/</c>. Damit zählen <c>/impressum</c> und
    /// <c>/impressum.html</c> als dieselbe Seite.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var value = path;

        var questionMark = value.IndexOf('?');
        if (questionMark >= 0)
        {
            value = value[..questionMark];
        }

        foreach (var extension in (string[])[".html", ".htm"])
        {
            if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^extension.Length];
                break;
            }
        }

        if (value.Length > 1 && value[^1] == '/')
        {
            value = value[..^1];
        }

        return value is "" or "/index" ? "/" : value;
    }

    /// <summary>Dateiendung des letzten Segments, inklusive Punkt; leer, wenn keine da ist.</summary>
    private static string Extension(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        var segment = path[(lastSlash + 1)..];
        var dot = segment.LastIndexOf('.');
        return dot <= 0 ? string.Empty : segment[dot..];
    }

    private static bool Contains(IReadOnlyList<string> extensions, string extension)
    {
        foreach (var candidate in extensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
