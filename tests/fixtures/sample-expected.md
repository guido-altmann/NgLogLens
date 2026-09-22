# Erwartete Ergebnisse für `sample-nginx.log`

Alle IPs stammen aus Dokumentationsbereichen (RFC 5737) bzw. sind öffentliche Crawler-Adressen.
Zeilennummern sind 1-basiert. Für die KI-Verifikation injizieren Tests einen IP-Range-Provider,
der `216.73.216.0/22` als Anthropic-Bereich enthält; `192.0.2.0/24` ist in keinem Bereich.

## Zeilentypen

| Typ | Zeilen | Anzahl |
|---|---|---|
| Access, vollständig | 1, 3, 5–30 | 28 |
| Access, gekürzt | 31 | 1 |
| Error, Duplikat „404.html failed“ | 2, 4 | 2 |
| Error, gekürzt | 32 | 1 |
| Unbekannt | – | 0 |

Anfragen gesamt: **29**.

## Klassifizierung je Zeile

| Zeile | Klasse | Kategorie | Agent / Verdikt | PageView | Monitoring | Grund (sinngemäß) |
|---|---|---|---|---|---|---|
| 1 | Attack | PHP-Webshells | – | nein | nein | 404 auf `.php` |
| 3 | Attack | WordPress | – | nein | nein | 404 auf `wp-` |
| 5 | Attack | Secrets & Credentials | – | nein | nein | 404 auf `.env` |
| 6 | Attack | Aufklärung | – | nein | nein | Scanner-IP 203.0.113.10 |
| 7 | AiAgent | – | ClaudeBot / Verified | nein | nein | UA + IP im Anthropic-Bereich |
| 8 | AiAgent | – | ClaudeBot / Verified | nein | nein | dito |
| 9 | Human | – | – | ja (`/`) | nein | Browser |
| 10 | Human | – | – | nein (CSS) | nein | Asset |
| 11 | Human | – | – | ja (`/ki-integration`) | nein | Browser |
| 12 | Human | – | – | nein | nein | Browser, 404 auf `favicon.ico` → Finding „Fehlende Assets“ |
| 13 | Human | – | – | ja (PDF) | **ja** | Paar mit Zeile 14 |
| 14 | Human | – | – | ja (PDF) | **ja** | Paar mit Zeile 13 |
| 15 | Human | – | – | ja (`/`) | **ja** | Paar mit Zeile 16 |
| 16 | Human | – | – | ja (`/`) | **ja** | Paar mit Zeile 15 |
| 17 | Bot | – | – | nein | nein | Googlebot |
| 18 | Bot | – | – | nein | nein | facebookexternalhit (206) |
| 19 | Attack | Vite-Dev-Server | ChatGPT-User / Spoofed | nein | nein | 404 auf `/@fs/` |
| 20 | Attack | Secrets & Credentials | Perplexity-User / Spoofed | nein | nein | 404 auf `appsettings` |
| 21 | Attack | Secrets & Credentials | – | nein | nein | Status 400 |
| 22 | Attack | Aufklärung | ClaudeBot / Spoofed | nein | nein | Scanner-IP 203.0.113.55 (llms.txt allein wäre kein Angriff) |
| 23 | Attack | Path Traversal & Injection | – | nein | nein | Injection in Query, Status 200 |
| 24 | AiAgent | – | PerplexityBot / Unverified | nein | nein | UA, IP in keinem Bereich |
| 25 | AiAgent | – | AgentTrustBot / Unverified | nein | nein | Agent-Discovery, kein Angriff |
| 26 | Attack | POST-Proben & Login-Versuche | – | nein | nein | POST, 405 |
| 27 | Attack | POST-Proben & Login-Versuche | – | nein | nein | POST |
| 28 | Attack | POST-Proben & Login-Versuche | – | nein | nein | POST (Methode vor WordPress) |
| 29 | Attack | Aufklärung | – | nein | nein | Scanner-IP 192.0.2.40 |
| 30 | Human | – | – | ja (`/impressum`) | nein | Browser, Referrer eigene Domain |
| 31 | AiAgent | – | ClaudeBot / Verified | nein | nein | gekürzte Zeile, Domain `anthropic.com`, IP im Bereich |

Hinweis 192.0.2.10 (Zeile 23): nur ein Einzelangriff, daher **kein** Scanner.

## Aggregate

- Klassen: Attack **13**, AiAgent **5**, Bot **2**, Human **9**
- Scanner-IPs: 203.0.113.10, 203.0.113.55, 192.0.2.40 (je 3 Einzelangriffe, je 4 Anfragen)
- Angriffskategorien: Secrets & Credentials 3, Aufklärung 3, POST-Proben & Login-Versuche 3,
  PHP-Webshells 1, WordPress 1, Vite-Dev-Server 1, Path Traversal & Injection 1
- Seitenaufrufe: **7** (ohne Monitoring: **3**)
- Top-Seiten: `/` 3, `/download/ki-reifegrad-analyse.pdf` 2, `/ki-integration` 1, `/impressum` 1
- Unterschiedliche /24-Netze mit Seitenaufrufen: 2 (ohne Monitoring: 1)
- Referrer ohne eigene Domain (`example.de`): `google.com` 1
- KI-Agenten: ClaudeBot 3 Verified / 1 Spoofed; ChatGPT-User 1 Spoofed; Perplexity-User 1 Spoofed;
  PerplexityBot 1 Unverified; AgentTrustBot 1 Unverified
- Statuscodes (vollständige Zeilen): 200 × 14, 404 × 10, 405 × 2, 400 × 1, 206 × 1
- Proxy-Instanzen: 10.0.1.9 (27.08.–31.08.), 10.0.1.4 (09.09.–13.09.), 10.0.1.8 (16.09.), 10.0.1.2 (21.09.–22.09.)

## Findings

Erwartet: Fehlende 404-Seite (2 Duplikate), Client-IP nur im Header, llms.txt fehlt (2 Anfragen),
Agent-Discovery-Nachfrage (1), .NET-Konfigdateien gesucht (1), Fehlende Assets (1).
Nicht erwartet: Scan-Bursts, Serverfehler.
