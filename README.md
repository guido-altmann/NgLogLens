# NgLogLens

Blazor-WebAssembly-Anwendung zum Öffnen, Bereinigen und Auswerten von nginx-Logs
aus einem Coolify-/Traefik-Export.

**Die Logdatei verlässt den Browser nicht.** Es gibt kein Backend, keine Telemetrie
und keine Analytics. Rohdaten werden nicht gespeichert; in `localStorage` liegen
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
| M6 | Empfehlungen, Einstellungen, globaler Zeitraumfilter | fertig |
| M7 | Performance-Benchmark, Dark Mode, Barrierefreiheit, Publish-Anleitung | fertig |

## Aufbau

```
src/LogLens.Core/   Fachlogik ohne UI- und I/O-Bezug (net10.0)
src/LogLens.Web/    Blazor WebAssembly standalone + MudBlazor
tests/              xUnit für Core, bUnit für Komponenten, Fixtures
docs/               Spezifikation und Referenz-Prototyp
deploy/             nginx-Konfiguration für den Betrieb
Dockerfile          Build und Auslieferung als nginx-Image (z. B. für Coolify)
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

## Veröffentlichen

NgLogLens ist nach dem Build eine rein statische Seite: `publish/wwwroot` genügt,
es gibt kein Backend. Jeder Webserver, der Dateien ausliefert, reicht aus. Die
folgenden Wege sind vorbereitet.

### Coolify (empfohlen)

1. In Coolify eine neue Ressource aus dem Git-Repository anlegen.
2. Als Build Pack **Dockerfile** wählen; das `Dockerfile` im Repository-Wurzelverzeichnis
   wird automatisch gefunden.
3. Unter *Network* den Port **80** eintragen (*Ports Exposes*).
4. Domain eintragen, z. B. `https://loglens.example.de`. TLS übernimmt der
   Coolify-Proxy (Traefik).
5. Deployen. Der Health-Check kann auf `/` zeigen.

Das Image baut die App mit dem .NET-10-SDK und liefert sie mit `nginx:stable-alpine`
aus, konfiguriert über [deploy/nginx.conf](deploy/nginx.conf) und
[deploy/security-headers.conf](deploy/security-headers.conf). Lokal ausprobieren:

```bash
docker build -t ngloglens .
docker run --rm -p 8080:80 ngloglens     # http://localhost:8080
```

### Eigener nginx-Server

```bash
dotnet publish src/LogLens.Web -c Release -o publish
rsync -a --delete publish/wwwroot/ user@server:/var/www/ngloglens/
```

Auf dem Server den `server`-Block aus [deploy/nginx.conf](deploy/nginx.conf)
übernehmen, `root` auf `/var/www/ngloglens` setzen und
[deploy/security-headers.conf](deploy/security-headers.conf) nach
`/etc/nginx/snippets/` legen (oder den `include`-Pfad anpassen). Danach
`nginx -t && systemctl reload nginx`.

`publish/` vorher leeren oder mit `--delete` synchronisieren: jeder Build erzeugt
neue Dateinamen mit Fingerabdruck, alte bleiben sonst liegen.

### Was die Konfiguration regelt

| Thema | Umsetzung |
|---|---|
| Routen der App | Unbekannte Pfade (`/overview`, `/attacks` …) liefern `index.html`; die App routet selbst. Fehlende Dateien unter `/_framework/` bleiben 404. |
| MIME-Typ WebAssembly | `.wasm` muss als `application/wasm` kommen. Die `mime.types` aktueller nginx-Versionen enthalten das; bei sehr alten Versionen dort ergänzen, nicht per `types`-Block im `server` (der ersetzt die ganze Tabelle). |
| Komprimierung | `dotnet publish` legt `.gz`- und `.br`-Fassungen ab. `gzip_static on` liefert die `.gz` aus. Für Brotli braucht nginx das Modul `ngx_brotli` und `brotli_static on`. |
| Caching | Dateien mit Fingerabdruck im Namen (`*.abcdef1234.wasm`) ein Jahr `immutable`; `index.html`, `blazor.webassembly.js`, `dotnet.js`, CSS und JS der App mit `no-cache`, damit nach einem Update nichts Altes zusammen mit Neuem läuft. |
| Content-Security-Policy | `connect-src 'self'`: der Browser verweigert jede Verbindung zu fremden Hosts, die Logdatei kann die Seite also auch technisch nicht verlassen. `'wasm-unsafe-eval'` braucht die .NET-Runtime, `'unsafe-eval'` die Achsen-Formatter von Blazor-ApexCharts, `style-src 'unsafe-inline'` MudBlazor. |
| Weitere Header | `X-Content-Type-Options`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `frame-ancestors 'none'`. |

Hinter einem weiteren Proxy (Traefik, Caddy) bleiben die Header von nginx erhalten;
dort keine zweite, abweichende CSP setzen.

## Leistung

SPEC 9 verlangt: 10 MB Log (~50.000 Zeilen) in unter 5 Sekunden, Release-Build ohne AOT.

- `PipelineBenchmarkTests` erzeugt ein solches Log (fest geseedet, nur
  Dokumentations-IPs) und misst die komplette Pipeline. Auf der JIT-Runtime des
  Test-Hosts dauert das rund 0,6 s.
- Im Browser interpretiert die WebAssembly-Runtime; dort ist es langsamer. Die
  Upload-Seite zeigt nach jeder Auswertung die *Dauer der Auswertung*.
  Gemessen im Release-Build (Edge, Apple M1): 3,3–4,2 s für 10 MB.

Wer mehr Luft braucht, kann mit AOT veröffentlichen (`dotnet workload install
wasm-tools`, dann `<RunAOTCompilation>true</RunAOTCompilation>`). Das vergrößert
den Download deutlich und ist deshalb nicht voreingestellt.

## Barrierefreiheit

- Tastatur: Sprungmarke „Zum Inhalt springen", sichtbarer Fokusrahmen, alle
  Bedienelemente per Tab erreichbar. Die Diagramme sind fokussierbar; ApexCharts
  bringt dafür eine eigene Tastatur-Navigation durch die Datenpunkte mit.
- Screenreader: eine `h1` je Seite, darunter `h2`/`h3` ohne Sprünge; jedes Diagramm
  hat eine Kurzbeschreibung, die Zahlen stehen zusätzlich in Legende oder Tabelle.
- Kontraste: beide Farbschemata erfüllen WCAG 2.2 AA (Text 4,5:1, Symbole 3:1),
  geprüft von `ThemeContrastTests`.
- Hell/Dunkel folgt dem System; die Wahl in der Kopfzeile oder unter *Einstellungen*
  bleibt gespeichert. Zoomen ist nicht gesperrt.

## Testdaten

`tests/fixtures/sample-nginx.log` ist anonymisiert und verwendet ausschließlich
Dokumentations-Adressbereiche sowie `example.de`.
`tests/fixtures/sample-expected.md` beschreibt das erwartete Ergebnis Zeile für
Zeile und ist die Referenz für die Tests. Echte Logs gehören nicht ins Repository.
