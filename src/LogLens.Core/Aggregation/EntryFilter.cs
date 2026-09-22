using System.Globalization;
using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Filter über die klassifizierten Anfragen (SPEC 8.8): Klasse, Status, IP, Pfad und
/// Zeitraum. Leere Felder filtern nicht; gesetzte Felder werden kombiniert.
/// </summary>
public sealed record EntryFilter
{
    public static EntryFilter None { get; } = new();

    /// <summary>Null oder leer: alle Klassen.</summary>
    public IReadOnlySet<TrafficClass>? Classes { get; init; }

    public StatusFilter? Status { get; init; }

    /// <summary>Teiltext der Client-IP oder ein Netz in CIDR-Schreibweise.</summary>
    public string? Ip { get; init; }

    /// <summary>Teiltext von Pfad oder Query, im Rohwert oder URL-dekodiert, ohne Groß-/Kleinschreibung.</summary>
    public string? Path { get; init; }

    /// <summary>Erster Tag (UTC), einschließlich.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Letzter Tag (UTC), einschließlich.</summary>
    public DateOnly? To { get; init; }

    public bool IsEmpty =>
        (Classes is null || Classes.Count == 0)
        && Status is null
        && string.IsNullOrWhiteSpace(Ip)
        && string.IsNullOrWhiteSpace(Path)
        && From is null
        && To is null;

    public IReadOnlyList<ClassifiedEntry> Apply(IReadOnlyList<ClassifiedEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (IsEmpty)
        {
            return entries;
        }

        // Einmal vorbereiten statt je Zeile: bei 50.000 Anfragen zählt das im Browser.
        var matcher = new Matcher(this);
        var result = new List<ClassifiedEntry>();

        foreach (var entry in entries)
        {
            if (matcher.Matches(entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    public bool Matches(ClassifiedEntry entry) => new Matcher(this).Matches(entry);

    private sealed class Matcher
    {
        private readonly IReadOnlySet<TrafficClass>? _classes;
        private readonly StatusFilter? _status;
        private readonly string? _ip;
        private readonly IpNetwork? _network;
        private readonly string? _path;
        private readonly DateOnly? _from;
        private readonly DateOnly? _to;

        public Matcher(EntryFilter filter)
        {
            _classes = filter.Classes is { Count: > 0 } classes ? classes : null;
            _status = filter.Status;
            _from = filter.From;
            _to = filter.To;

            var ip = filter.Ip?.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                if (ip.Contains('/', StringComparison.Ordinal) && IpNetwork.TryParse(ip, out var network))
                {
                    _network = network;
                }
                else
                {
                    _ip = ip;
                }
            }

            var path = filter.Path?.Trim();
            _path = string.IsNullOrEmpty(path) ? null : path;
        }

        public bool Matches(ClassifiedEntry classified)
        {
            var entry = classified.Entry;

            if (_classes is not null && !_classes.Contains(classified.Class))
            {
                return false;
            }

            if (_status is not null && !_status.Matches(entry.Status))
            {
                return false;
            }

            if (_network is not null && !_network.Contains(entry.ClientIp))
            {
                return false;
            }

            if (_ip is not null && !entry.ClientIp.Contains(_ip, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_from is not null || _to is not null)
            {
                var day = DateOnly.FromDateTime(entry.Timestamp.UtcDateTime);
                if (day < _from || day > _to)
                {
                    return false;
                }
            }

            return _path is null || MatchesPath(entry);
        }

        private bool MatchesPath(AccessEntry entry)
        {
            var target = entry.Query is null ? entry.Path : $"{entry.Path}?{entry.Query}";
            if (target is null)
            {
                return false;
            }

            return target.Contains(_path!, StringComparison.OrdinalIgnoreCase)
                || UrlText.Decode(target).Contains(_path!, StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// Statusfilter aus einer Eingabe wie <c>404</c>, <c>4xx</c> oder <c>2xx, 405</c>.
/// Zeilen ohne Status (gekürzt) passen auf keinen Statusfilter.
/// </summary>
public sealed class StatusFilter
{
    private const int MinStatus = 100;
    private const int MaxStatus = 599;

    private readonly HashSet<int> _codes;
    private readonly HashSet<int> _classes;

    private StatusFilter(HashSet<int> codes, HashSet<int> classes)
    {
        _codes = codes;
        _classes = classes;
    }

    /// <summary>
    /// Leere Eingabe ergibt <c>true</c> und <paramref name="filter"/> = null: kein Filter.
    /// </summary>
    public static bool TryParse(string? text, out StatusFilter? filter)
    {
        filter = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var codes = new HashSet<int>();
        var classes = new HashSet<int>();

        foreach (var part in text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParsePart(part, codes, classes))
            {
                return false;
            }
        }

        filter = new StatusFilter(codes, classes);
        return true;
    }

    public bool Matches(int? status) =>
        status is int value && (_codes.Contains(value) || _classes.Contains(value / 100));

    private static bool TryParsePart(string part, HashSet<int> codes, HashSet<int> classes)
    {
        if (part.Length == 3 && part.EndsWith("xx", StringComparison.OrdinalIgnoreCase)
            && part[0] is >= '1' and <= '5')
        {
            classes.Add(part[0] - '0');
            return true;
        }

        if (part.Length == 3
            && int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            && code is >= MinStatus and <= MaxStatus)
        {
            codes.Add(code);
            return true;
        }

        return false;
    }
}
