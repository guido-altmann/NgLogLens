# LogLens

Blazor-WebAssembly-App zum Öffnen, Bereinigen und Auswerten von nginx-Logs
(Coolify/Traefik-Export). Die Logdatei wird ausschließlich im Browser verarbeitet.

Die fachliche Spezifikation steht in `docs/SPEC.md`. Lies sie, bevor du ein Feature
planst oder umsetzt. Bei Widersprüchen gilt SPEC.md; sprich den Widerspruch an.

## Tech-Stack

- .NET 10 (LTS), C# mit `<Nullable>enable</Nullable>` und `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- Blazor WebAssembly **standalone** (kein Server-Projekt, kein Hosted-Template)
- MudBlazor (aktuelle stabile Version, die .NET 10 unterstützt; vor dem Einbinden prüfen)
- Charts: Blazor-ApexCharts
- Tests: xUnit, bUnit für Komponenten. Assertions mit xUnit-`Assert` oder Shouldly, **nicht** FluentAssertions ab v8 (kommerzielle Lizenz)

Neue NuGet-Pakete nur nach Rückfrage.

## Solution-Struktur

```
LogLens.sln
src/
  LogLens.Core/        Class Library (net10.0), keine UI- oder Blazor-Abhängigkeit
    Parsing/           Zeilenparser für Access-, Error- und gekürzte Zeilen
    Classification/    Angriffs-, Bot-, KI- und Monitoring-Erkennung
    Aggregation/       Kennzahlen, Zeitreihen, Top-Listen
    Findings/          Regelbasierte Konfigurationsempfehlungen
    Models/            Immutable records
    Resources/         Eingebettete JSON-Dateien (Pattern-Listen, KI-IP-Bereiche)
  LogLens.Web/         Blazor WASM + MudBlazor
tests/
  LogLens.Core.Tests/
  LogLens.Web.Tests/   bUnit
  fixtures/            Anonymisierte Beispiel-Logs + erwartete Ergebnisse
docs/
  SPEC.md
  reference/           Python-Prototyp der Klassifizierung (nur Referenz)
```

## Architekturregeln

- `LogLens.Core` ist frei von I/O-Details: Eingabe ist `TextReader` bzw. `IAsyncEnumerable<string>`,
  Ausgabe sind Records. Dadurch ist Core später in CLI, Worker oder API wiederverwendbar.
- Verarbeitung als Pipeline: **Parse → Dedupe → Classify → Aggregate → Findings**.
  Jede Stufe ist eigenständig testbar.
- Klassifizierung braucht zwei Durchläufe (Scanner-IPs erst nach Sicht auf alle Einträge
  bekannt). Die Aggregation arbeitet auf der klassifizierten Liste im Speicher.
- Alle Regex als `[GeneratedRegex]` (Source Generator). `RegexOptions.Compiled` bringt unter WASM nichts.
- Pattern-Listen (Angriffspfade, Bot-User-Agents, KI-Agenten) liegen als eingebettetes JSON in
  `Resources/`, nicht hart codiert in Klassen. Jede Liste hat ein Feld `updated`.
- Parsing mit `CultureInfo.InvariantCulture`, Zeitangaben intern als `DateTimeOffset` in UTC.
  Anzeige in `de-DE`.
- Services per DI registrieren; Core stellt `AddLogLensCore()` als Extension bereit.

## Datenschutz (nicht verhandelbar)

- Die Logdatei verlässt nie den Browser. Keine HTTP-Aufrufe mit Loginhalten, keine Telemetrie,
  kein Analytics.
- Rohdaten werden nicht in `localStorage`/IndexedDB gespeichert. Persistiert werden nur Einstellungen.
- Besucher-IPs werden in der UI standardmäßig auf /24 (IPv4) bzw. /48 (IPv6) maskiert.
  Scanner-IPs werden vollständig angezeigt (für Blocklisten). Umschaltbar in den Einstellungen.
- Test-Fixtures verwenden nur Dokumentations-IP-Bereiche (RFC 5737: 192.0.2.0/24,
  198.51.100.0/24, 203.0.113.0/24) und `example.de`. Echte Logs niemals committen;
  `*.log` außerhalb von `tests/fixtures/` steht in `.gitignore`.

## Blazor-WASM-Fallstricke

- `IBrowserFile.OpenReadStream()` hat ein Standardlimit von 500 KB. Limit explizit setzen
  (konfigurierbar, Default 200 MB) und zeilenweise per `StreamReader` lesen.
- WASM hat einen UI-Thread. Beim Parsen alle ~2.000 Zeilen `await Task.Yield()` und Fortschritt
  melden (`IProgress<ParseProgress>`), Abbruch über `CancellationToken`.
- Große Tabellen nur mit Virtualisierung (`MudDataGrid` mit `Virtualize`).
- Keine synchronen Blocking-Aufrufe (`.Result`, `.Wait()`).

## Arbeitsweise

- Vor jedem Meilenstein: Plan vorlegen (betroffene Dateien, Klassen, Tests), dann umsetzen.
- Core wird testgetrieben entwickelt: erst Test gegen `tests/fixtures/`, dann Implementierung.
- `tests/fixtures/sample-expected.md` beschreibt das Soll für jede Fixture-Zeile. Weicht ein Test
  davon ab, nicht die Erwartung anpassen, sondern nachfragen.
- Nach jeder Änderung: `dotnet build` und `dotnet test` müssen grün sein, bevor du „fertig“ meldest.
- Kleine, thematisch saubere Commits mit aussagekräftiger Nachricht (Deutsch oder Englisch, einheitlich).
- Offene Fragen oder Annahmen am Ende deiner Antwort auflisten, nicht stillschweigend entscheiden.

## Code-Stil

- File-scoped namespaces, `sealed` wo möglich, Records für Datenmodelle
- Primary Constructors für Services
- Async-Methoden enden auf `Async` und nehmen einen `CancellationToken`
- Keine Magic Numbers: Schwellwerte gehören in `ClassificationOptions`
- UI-Texte auf Deutsch, Code-Bezeichner auf Englisch
- Razor-Komponenten: Logik in Code-Behind (`.razor.cs`) sobald sie über ein paar Zeilen hinausgeht

## Befehle

```bash
dotnet build
dotnet test
dotnet run --project src/LogLens.Web
dotnet publish src/LogLens.Web -c Release -o publish   # statische Dateien in publish/wwwroot
```
