namespace LogLens.Core;

/// <summary>
/// Einstellungen des Parsers. Alle Grenzwerte stehen hier, nicht im Code.
/// </summary>
public sealed class ParserOptions
{
    /// <summary>Nach so vielen Zeilen wird der UI-Thread freigegeben und Fortschritt gemeldet.</summary>
    public int YieldInterval { get; set; } = 2_000;

    /// <summary>Höchstzahl gesammelter Beispiele für nicht erkannte Zeilen (SPEC 2.4).</summary>
    public int MaxUnknownSamples { get; set; } = 50;

    /// <summary>Höchstlänge eines gespeicherten Beispiels einer unbekannten Zeile.</summary>
    public int UnknownSampleLength { get; set; } = 300;

    /// <summary>Maximale Dateigröße beim Einlesen im Browser (SPEC/CLAUDE.md: Default 200 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>
    /// Lesepuffer des <see cref="StreamReader"/>. Im Browser ist jeder Lesezugriff ein
    /// JS-Interop-Aufruf, deshalb deutlich größer als der .NET-Standard von 1 KB.
    /// </summary>
    public int ReadBufferSizeBytes { get; set; } = 64 * 1024;

    /// <summary>Höchstzahl Dateien, die in einem Rutsch geöffnet werden (SPEC 8.1).</summary>
    public int MaxFileCount { get; set; } = 20;

    /// <summary>
    /// Dateiname der Fehlerseite. Error-Zeilen „open() …/&lt;Name&gt; failed" sind
    /// Folgefehler eines 404 und werden aussortiert (SPEC 3).
    /// </summary>
    public string NotFoundPageFileName { get; set; } = "404.html";
}

/// <summary>
/// Schwellwerte der Klassifizierung (SPEC 5). Keine Magic Numbers im Code: was hier
/// nicht steht, ist keine Stellschraube.
/// </summary>
public sealed class ClassificationOptions
{
    /// <summary>Nach so vielen Anfragen wird der UI-Thread freigegeben und Fortschritt gemeldet.</summary>
    public int YieldInterval { get; set; } = 2_000;

    /// <summary>Ab so vielen Einzelangriffen gilt eine Client-IP als Scanner (SPEC 5.2).</summary>
    public int ScannerThreshold { get; set; } = 3;

    /// <summary>Statuscodes, die für sich allein schon ein Angriff sind (SPEC 5.1.2).</summary>
    public IReadOnlyList<int> AttackStatusCodes { get; set; } = [400, 405];

    /// <summary>Status, bei dem ein Angriffspfad zum Angriff wird (SPEC 5.1.3).</summary>
    public int AttackPathStatusCode { get; set; } = 404;

    /// <summary>
    /// Methoden, die als normales Lesen gelten (SPEC 5.1.1). Alles andere ist ein Angriff.
    /// Zeilen ohne erkennbare Methode (gekürzt oder ungültig, SPEC 2.1/2.3) lösen die
    /// Regel nicht aus – aus ihnen lässt sich nichts ableiten.
    /// </summary>
    public IReadOnlyList<string> ReadMethods { get; set; } = ["GET", "HEAD"];

    /// <summary>Endungen, die als Seite zählen (SPEC 5.7).</summary>
    public IReadOnlyList<string> PageExtensions { get; set; } = [".html", ".htm"];

    /// <summary>Downloads, die als Seitenaufruf zählen (SPEC 5.7).</summary>
    public IReadOnlyList<string> DownloadExtensions { get; set; } = [".pdf"];

    /// <summary>Zeitfenster, in dem zwei Abrufe als gleichzeitig gelten (SPEC 5.8).</summary>
    public TimeSpan MonitoringWindow { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>So viele verschiedene Zeitpunkte muss ein IP-Paar zeigen (SPEC 5.8).</summary>
    public int MonitoringMinOccurrences { get; set; } = 2;

    /// <summary>Netzgröße für „im selben Netz" bei IPv4 (SPEC 5.8: /16).</summary>
    public int MonitoringIpv4PrefixLength { get; set; } = 16;

    /// <summary>
    /// Gegenstück für IPv6. SPEC 5.8 nennt nur /16; ein IPv6-/16 wäre ein ganzes
    /// Registry-Segment, deshalb hier das zur /48-Maskierung passende /32.
    /// </summary>
    public int MonitoringIpv6PrefixLength { get; set; } = 32;

    /// <summary>Vom Nutzer als eigenes Monitoring markierte IPs oder Netze (SPEC 5.8).</summary>
    public IList<string> MonitoringNetworks { get; } = [];
}

/// <summary>
/// Stellschrauben der Aggregation (SPEC 6). Die Netzgrößen sind dieselben wie bei der
/// IP-Maskierung in der Oberfläche (CLAUDE.md, Datenschutz): /24 für IPv4, /48 für IPv6.
/// </summary>
public sealed class AggregationOptions
{
    /// <summary>Netzgröße für „unterschiedliche Netze" bei IPv4.</summary>
    public int VisitorNetworkIpv4PrefixLength { get; set; } = 24;

    /// <summary>Netzgröße für „unterschiedliche Netze" bei IPv6.</summary>
    public int VisitorNetworkIpv6PrefixLength { get; set; } = 48;

    /// <summary>Länge der Top-Listen: Seiten, Referrer, Netze, Seiten je KI-Agent.</summary>
    public int TopListSize { get; set; } = 20;

    /// <summary>
    /// Eigene Domains; Referrer von dort zählen nicht als Herkunft (SPEC 6). Subdomains
    /// gehören dazu. Zusätzlich gelten die Hosts aus den Error-Zeilen des Logs.
    /// </summary>
    public IList<string> OwnDomains { get; } = [];
}
