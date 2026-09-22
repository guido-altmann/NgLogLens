# LogLens – Fachliche Spezifikation

## 1. Ziel

Ein Nutzer öffnet einen nginx-Log-Export im Browser und bekommt eine **bereinigte** Sicht:
Wie viele echte Menschen haben die Seite besucht, was lesen sie, welche Angriffe laufen,
welche KI-Crawler sind echt, und was sollte an der Server-Konfiguration geändert werden.

Referenz: Ein Prototyp in Python (`docs/reference/prototype_classifier.py`) hat ein echtes
Log (7.836 Zeilen, 27.08.–22.09.2026) ausgewertet. Er dient als Ausgangspunkt, enthält aber
bekannte Fehler (siehe 5.8). Die Regeln in dieser Spezifikation gehen vor.

## 2. Eingabeformat

Ein Coolify-Export mischt Access- und Error-Log in einer Datei, zeitlich grob sortiert.
Nicht jede Zeile ist vollständig. Der Parser muss vier Zeilentypen erkennen:

### 2.1 Access-Zeile (nginx `combined` + X-Forwarded-For)

```
10.0.1.9 - - [27/Aug/2026:10:16:58 +0000] "GET /0x.php HTTP/1.1" 404 555 "-" "Mozilla/5.0 ..." "203.0.113.10"
```

Felder: `ProxyIp`, `RemoteUser`, `Timestamp`, `Method`, `Path`, `Protocol`, `Status`, `Bytes`,
`Referer`, `UserAgent`, `ClientIp` (letztes Feld = X-Forwarded-For).

- Enthält X-Forwarded-For mehrere IPs (`"a, b"`), gilt die **erste** als Client-IP.
- Fehlt das letzte Feld (reines `combined`-Format), ist `ClientIp = ProxyIp`, und ein Finding
  „Keine Client-IP im Log“ wird erzeugt.
- Pfad und Query getrennt speichern (`Path`, `Query`). URL-Dekodierung nur für die Anzeige,
  die Erkennung arbeitet auf dem Rohwert und dem dekodierten Wert.
- Ungültige Request-Zeilen (z. B. Binärmüll, leerer Request `"-"`) werden als Access-Zeile mit
  `Method = null` übernommen.

### 2.2 Error-Zeile

```
2026/08/27 10:16:58 [error] 30#30: *728 open() "/usr/share/nginx/html/404.html" failed (2: No such file or directory), client: 10.0.1.9, server: , request: "GET /0x.php HTTP/1.1", host: "example.de"
```

Felder: `Timestamp`, `Level`, `Message`, `Request`, `Host`.

### 2.3 Gekürzte Zeilen

Manche Exporte kürzen Zeilen (z. B. `<REDACTED>` mitten im User-Agent). Regel:
- Beginnt die Zeile wie eine Access-Zeile und endet mit `"<IP>"`, wird ein Access-Eintrag mit
  Datum, Proxy-IP und Client-IP erzeugt; Status und Pfad sind `null`, `IsTruncated = true`.
  Enthält der Rest eine Kontaktdomain (`@anthropic.com`, `@bytedance.com` …), wird sie für die
  Agent-Erkennung verwendet.
- Beginnt die Zeile wie eine Error-Zeile, wird sie als gekürzte Error-Zeile gezählt.

### 2.4 Unbekannte Zeilen

Werden gezählt und in einer Diagnoseliste (max. 50 Beispiele) gesammelt, nie verworfen ohne Zählung.

## 3. Bereinigung (Dedupe)

- Error-Zeilen vom Typ `open() "…/404.html" failed` sind Folgefehler eines 404 und werden
  **nicht** als eigene Ereignisse gezählt. Ihre Anzahl wird für ein Finding festgehalten.
- Andere Error-Zeilen bleiben als eigene Ereignisliste erhalten und werden in „Server-Zustand“ angezeigt.
- Eine „Anfrage“ ist genau eine Access-Zeile (vollständig oder gekürzt).

## 4. Datenmodell (Richtwert)

```csharp
public sealed record AccessEntry(
    int LineNumber, DateTimeOffset Timestamp, TimePrecision TimePrecision,
    string ProxyIp, string ClientIp, bool HasClientIpHeader,
    string? Method, string? Path, string? Query, string? Protocol, int? Status, long? Bytes,
    string? Referer, string? UserAgent, bool IsTruncated);

// Gekürzte Zeilen (2.3) enthalten oft nur Datum oder Datum und Stunde. Der Zeitstempel
// wird dann auf den Beginn der bekannten Einheit gesetzt; TimePrecision hält fest, wie
// genau er ist. Auswertungen je Stunde (6.) verwenden nur Einträge ab Hour.
public enum TimePrecision { Second, Minute, Hour, Day }

public enum TrafficClass { Human, AiAgent, Bot, Attack }

public enum AiVerdict { Verified, Unverified, Spoofed }

public sealed record ClassifiedEntry(
    AccessEntry Entry, TrafficClass Class, string? AttackCategory,
    string? AgentName, AiVerdict? AiVerdict, bool IsPageView,
    bool IsMonitoringSuspected, string Reason);
```

`Reason` ist ein kurzer, menschenlesbarer Grund („Scanner-IP“, „POST auf statische Seite“,
„404 auf Angriffspfad: .env“). Er wird in der Rohdaten-Ansicht angezeigt und macht die
Heuristik nachvollziehbar.

## 5. Klassifizierung

Reihenfolge der Prüfung ist verbindlich. Alle Schwellwerte in `ClassificationOptions`.

### 5.1 Einzelanfrage ist ein Angriff, wenn

1. `Method` nicht `GET` oder `HEAD` ist, **oder**
2. `Status` 400 oder 405 ist, **oder**
3. `Status` 404 ist **und** der Pfad auf ein Angriffsmuster (5.3) passt, **oder**
4. die Query ein Injection-Muster enthält (unabhängig vom Status; nginx liefert bei
   statischen Seiten 200, weil die Query ignoriert wird).

### 5.2 Scanner-Regel

Eine Client-IP mit **≥ 3** Einzelangriffen (Option `ScannerThreshold`) ist ein Scanner.
**Alle** Anfragen dieser IP werden als `Attack` klassifiziert, auch unauffällige (Kategorie
„Aufklärung“ für Anfragen, die für sich allein kein Angriff wären).

### 5.3 Angriffskategorien

Erste passende Kategorie gewinnt. Muster case-insensitive.

| Kategorie | Muster (Pfad/Query) |
|---|---|
| POST-Proben & Login-Versuche | Method ≠ GET/HEAD; oder `/signin`, `/signup`, `/account`, `/login` |
| WordPress | `wp-`, `wordpress`, `xmlrpc`, `rest_route` |
| Vite-Dev-Server | `/@fs/`, `/@vite/` |
| Secrets & Credentials | `.env`, `credentials`, `.aws`, `.ssh`, `.git`, `.svn`, `appsettings`, `local.settings.json`, `web.config`, `secrets`, `*key*.json`, `sa.json`, `creds`, `terraform`, `rootkey`, `firebase`, `sftp`, `.vscode`, `*.yml`/`*.yaml` im Root, `server.key`, `id_rsa`, `id_dsa` |
| PHP-Webshells | `.php`, `phpinfo`, `_profiler` |
| Framework-Endpunkte | `actuator`, `_ignition`, `telescope`, `trace.axd`, `elmah.axd`, `/info` |
| Path Traversal & Injection | `..`, `%2e%2e`, `%2f` im Pfad, `proc/self`, `/etc/`, `cgi-bin`, Query mit `cmd=`, `command=`, `` ` `` bzw. `%60`, `;echo`, `GSCAN` |
| Aufklärung | nur über Scanner-Regel (5.2) |
| Sonstige Proben | Rest |

Die Muster liegen in `Resources/attack-patterns.json`.

### 5.4 Agent-Discovery ist kein Angriff

Anfragen auf `/llms.txt`, `/llms-full.txt`, `/.well-known/agents.json`,
`/.well-known/agent-card.json`, `/.well-known/mcp/server-card.json`, `/.well-known/ai-plugin.json`
gelten **nicht** als Angriffsmuster, auch bei 404. Sie werden als Nachfrage für ein Finding gezählt.
(Ausnahme: Stammen sie von einer Scanner-IP, greift 5.2.)

### 5.5 KI-Agenten

Erkennung über User-Agent oder Kontaktdomain gekürzter Zeilen. Mindestens:
ClaudeBot, Claude-User, Claude-SearchBot, GPTBot, OAI-SearchBot, ChatGPT-User,
PerplexityBot, Perplexity-User, Google-Extended, Applebot(-Extended), Bytespider,
meta-externalagent, CCBot, Amazonbot, AgentTrustBot und vergleichbare Agent-Discovery-Crawler.

Verdikt:
- `Spoofed`: Anfrage ist als `Attack` klassifiziert (5.1/5.2). Der User-Agent dient als Tarnung.
- `Verified`: Client-IP liegt in einem veröffentlichten IP-Bereich des Anbieters.
- `Unverified`: sonst.

IP-Bereiche liegen als `Resources/ai-ip-ranges.json` mit Quelle und Stand je Anbieter. Recherchiere
beim Umsetzen die aktuell veröffentlichten Quellen (mehrere Anbieter stellen JSON-Listen bereit) und
dokumentiere sie in `docs/ip-range-sources.md`. Die App lädt diese Listen **nicht** zur Laufzeit nach
(Datenschutz, Offline-Fähigkeit); Aktualisierung erfolgt per Build.

### 5.6 Such- und sonstige Bots

User-Agent enthält `bot`, `crawl`, `spider`, `slurp`, `preview`, `facebookexternalhit`,
`censys`, `curl`, `python`, `go-http`, `wget`, `headless`, `uptime`, `monitor`, oder ist leer bzw. `-`
oder exakt `Mozilla/5.0`. Liste in `Resources/bot-patterns.json`.

### 5.7 Mensch

Alles andere, **unabhängig vom Status**. Ein Browser, der `/favicon.ico` mit 404 bekommt, ist ein
Mensch mit fehlendem Asset, kein Bot.

`IsPageView = true`, wenn Class = Human, Status 2xx/304 und der Pfad eine Seite ist:
`/`, `*.html`, Pfade ohne Dateiendung, oder Downloads (`*.pdf`, konfigurierbare Endungen).
Keine Bilder, Fonts, CSS, JS, Sitemaps.

Pfad-Normalisierung für Seitenstatistiken: Query entfernen, `.html` entfernen, `/index` → `/`.

### 5.8 Monitoring-Verdacht

Zwei verschiedene Client-IPs im selben /16-Netz, die denselben Pfad innerhalb von ≤ 2 Sekunden
abrufen, und das an ≥ 2 verschiedenen Zeitpunkten → beide IPs bekommen
`IsMonitoringSuspected = true`. Zusätzlich kann der Nutzer IPs/Netze manuell als „eigenes Monitoring“
markieren. Die UI bietet einen Schalter „Monitoring aus Seitenaufrufen herausrechnen“.

### 5.9 Bekannte Fehler des Prototyps (nicht übernehmen)

- `.json`-Endung allein war Angriffsmuster → Agent-Discovery-Dateien wurden als Angriff gewertet (siehe 5.4).
- 4xx-Anfragen von Browsern wurden als Bot gezählt (siehe 5.7).
- Keine IP-Verifikation für KI-Agenten.

## 6. Aggregation

- Zeitraum, Anzahl Zeilen gesamt, Anfragen, entfernte Duplikate, gekürzte und unbekannte Zeilen
- Anfragen je `TrafficClass` gesamt und pro Tag (lückenlose Tagesreihe, auch Tage mit 0)
- Anfragen je Stunde (UTC) für Human und Attack
- Seitenaufrufe, Anzahl unterschiedlicher /24-Netze, Top-Seiten, Top-Referrer (eigene Domain ausgeschlossen)
- Angriffe je Kategorie, Top-Scanner (IP, Anzahl, erster/letzter Zeitpunkt, Hauptkategorie, Dauer des Bursts)
- KI-Agenten je Name mit Verteilung der Verdikte
- Statuscodes, Serverfehler (5xx)
- Proxy-Instanzen (Proxy-IP mit erstem/letztem Tag) als Hinweis auf Redeployments
- Tage ohne Einträge

## 7. Findings (Empfehlungen)

Regelbasiert, jede Regel eine Klasse mit `Evaluate(AnalysisResult) : Finding?`. Finding hat
Priorität (Hoch/Mittel/Niedrig), Titel, Begründung mit Zahlen und einen Konfigurations-Snippet.

| Regel | Auslöser | Snippet |
|---|---|---|
| Fehlende 404-Seite | ≥ 1 Duplikat-Fehlerzeile „404.html failed“ | `error_page`/`log_not_found off;` |
| Client-IP nur im Header | alle Proxy-IPs privat (RFC 1918) | `set_real_ip_from` + `real_ip_header X-Forwarded-For;` |
| Scan-Bursts | Scanner mit ≥ 100 Anfragen in ≤ 5 Minuten | Traefik-RateLimit-Middleware bzw. CrowdSec |
| llms.txt fehlt | ≥ 1 Anfrage auf `/llms.txt` mit 404 | Hinweis + Beispielstruktur |
| Agent-Discovery-Nachfrage | Anfragen auf `.well-known`-Agent-Dateien | Hinweis |
| .NET-Konfigdateien gesucht | Anfragen auf `appsettings*.json`, `local.settings.json`, `web.config` | Prüfen, dass Publish-Ordner keine Konfig enthält |
| Fehlende Assets | Browser-404 auf `favicon.ico`, `apple-touch-icon*`, `*.map` | Hinweis |
| Serverfehler | ≥ 1 × 5xx | Liste der betroffenen Pfade |

## 8. Oberfläche

MudBlazor-Layout mit Navigationsleiste. Hell/Dunkel folgt dem System, umschaltbar.

1. **Start / Upload**: Drag & Drop oder Dateiauswahl, mehrere Dateien möglich (werden zusammengeführt,
   Duplikate über identische Zeilen erkannt). Fortschrittsbalken mit Zeilenzahl, Abbrechen-Button.
   Hinweis „Die Datei verlässt deinen Browser nicht“.
2. **Übersicht**: Kernaussage als Satz („Von X Anfragen waren Y Seitenaufrufe von Menschen“),
   Anteilsbalken der vier Klassen, Tagesverlauf gestapelt mit Umschalter
   „Alle / Ohne Angriffe / Nur Menschen“.
3. **Besucher**: Top-Seiten, Referrer, Tageszeit, aktivste Netze (maskiert), Monitoring-Schalter.
4. **Angriffe**: Kategorien, Scanner-Tabelle, Export der Scanner-IPs als nginx-`deny`-Liste,
   Klartext-Liste und CSV.
5. **KI-Agenten**: pro Agent Balken Verifiziert/Unverifiziert/Getarnt, Liste der abgerufenen Seiten
   echter Agenten.
6. **Server-Zustand**: Statuscodes, 5xx, übrige Error-Zeilen, Proxy-Instanzen als Zeitleiste, Parser-Diagnose.
7. **Empfehlungen**: Findings sortiert nach Priorität, Snippets mit Kopier-Button.
8. **Rohdaten**: virtualisiertes `MudDataGrid` mit Filter nach Klasse, Status, IP, Pfad, Zeitraum;
   Spalte „Grund“ aus `Reason`.
9. **Einstellungen**: Schwellwerte, IP-Maskierung, eigene Monitoring-Netze, eigene Domain(s),
   max. Dateigröße. Persistiert in `localStorage` (nur Einstellungen).

Globaler Zeitraumfilter wirkt auf alle Ansichten.

## 9. Nicht-funktionale Anforderungen

- 10 MB Log (~50.000 Zeilen) in unter 5 Sekunden auf einem aktuellen Laptop verarbeitet
  (Release-Build, ohne AOT). Benchmark als Test mit generiertem Log.
- UI bleibt während des Parsens bedienbar.
- Läuft als statische Seite hinter nginx; `publish/wwwroot` genügt.
- Keine externen Laufzeit-Requests außer den App-Assets selbst.
- Barrierearm: Tastaturbedienung, Kontraste, Charts mit Textalternative.

## 10. Meilensteine

Jeder Meilenstein endet mit grünem Build, grünen Tests und kurzer Zusammenfassung.

**M1 – Gerüst**: Solution, Projekte, DI, MudBlazor-Layout mit leeren Seiten, CI-fähiges `dotnet test`,
`.gitignore`, `README.md`.
*Abnahme*: App startet, Navigation funktioniert, ein Dummy-Test läuft.

**M2 – Parser**: Access-, Error-, gekürzte und unbekannte Zeilen; Dedupe.
*Abnahme*: Tests gegen `tests/fixtures/sample-nginx.log` bestätigen Zeilentypen und Zählwerte
aus `sample-expected.md`.

**M3 – Klassifizierung**: Regeln 5.1–5.8 inkl. JSON-Ressourcen und `ClassificationOptions`.
*Abnahme*: jede Fixture-Zeile hat Klasse, Kategorie, Agent, Verdikt, PageView und Monitoring-Flag
wie in `sample-expected.md`.

**M4 – Upload & Übersicht**: Datei-Upload mit Fortschritt/Abbruch, Übersichtsseite mit Charts.
*Abnahme*: Fixture-Datei lässt sich öffnen, Zahlen stimmen mit M3 überein; bUnit-Test für Upload-Komponente.

**M5 – Detailseiten**: Besucher, Angriffe (inkl. Export), KI-Agenten, Server-Zustand, Rohdaten.

**M6 – Findings & Einstellungen**: Regeln aus Abschnitt 7, Einstellungsseite, Zeitraumfilter.

**M7 – Feinschliff**: Performance-Benchmark (Abschnitt 9), Dark Mode, Barrierefreiheit,
Publish-Anleitung für nginx/Coolify im README.

## 11. Nicht im Umfang (vorerst)

- Live-Anbindung an Coolify, Docker oder Server-Dateisysteme
- Persistenz von Auswertungen über Sitzungen hinweg
- Andere Log-Formate (Apache, Traefik-JSON, IIS). Die Parser-Schnittstelle soll aber erweiterbar sein
  (`ILogLineParser`).
