# NgLogLens

Blazor-WebAssembly-Anwendung zum Öffnen, Bereinigen und Auswerten von nginx-Logs
aus einem Coolify-/Traefik-Export.

**Die Logdatei verlässt den Browser nicht.** Es gibt kein Backend, keine Telemetrie
und keine Analytics. Rohdaten werden nicht gespeichert; persistiert werden später
ausschließlich Einstellungen.

Die fachliche Spezifikation steht in [docs/SPEC.md](docs/SPEC.md).

## Stand

| Meilenstein | Inhalt | Status |
|---|---|---|
| M1 | Solution, Projekte, DI, MudBlazor-Layout mit leeren Seiten | fertig |
| M2 | Parser für Access-, Error-, gekürzte und unbekannte Zeilen, Dedupe | fertig |
| M3 | Klassifizierung | fertig |
| M4 | Upload und Übersicht | fertig |
| M5 | Detailseiten: Besucher, Angriffe mit Export, KI-Agenten, Server-Zustand, Rohdaten | fertig |
| M6 | Findings und Einstellungen | offen |
| M7 | Feinschliff | offen |

## Aufbau

```
src/LogLens.Core/   Fachlogik ohne UI- und I/O-Bezug (net10.0)
src/LogLens.Web/    Blazor WebAssembly standalone + MudBlazor
tests/              xUnit für Core, bUnit für Komponenten, Fixtures
docs/               Spezifikation und Referenz-Prototyp
```

## Voraussetzungen

.NET SDK 10.0 oder neuer. Eine Workload-Installation ist nicht nötig, solange
nicht AOT-kompiliert wird.

## Befehle

```bash
dotnet build
dotnet test
dotnet run --project src/LogLens.Web
dotnet publish src/LogLens.Web -c Release -o publish   # statische Dateien in publish/wwwroot
```

## Testdaten

`tests/fixtures/sample-nginx.log` ist anonymisiert und verwendet ausschließlich
Dokumentations-Adressbereiche sowie `example.de`.
`tests/fixtures/sample-expected.md` beschreibt das erwartete Ergebnis Zeile für
Zeile und ist die Referenz für die Tests. Echte Logs gehören nicht ins Repository.
