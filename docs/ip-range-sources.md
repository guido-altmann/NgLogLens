# Quellen der KI-IP-Bereiche

`src/LogLens.Core/Resources/ai-ip-ranges.json` entscheidet, ob ein KI-Agent das Verdikt
`Verified` oder `Unverified` bekommt (SPEC 5.5). Diese Datei hält fest, woher die Bereiche
stammen, wie sie zusammengesetzt werden und was bei Anbietern ohne Liste gilt.

**Die App lädt zur Laufzeit nichts nach.** Die Liste ist eine eingebettete Ressource;
aktualisiert wird sie per Build durch einen Menschen, der die Quellen abruft und das
Ergebnis committet. Das ist Absicht: die Logdatei verlässt den Browser nicht, und die App
soll offline funktionieren.

## Stand

Abgerufen am **2026-09-22**. `updated` in der JSON-Datei nennt dieses Datum,
`sourceUpdated` je Anbieter den Zeitstempel, den die Quelle selbst mitliefert.

## Anbieter mit veröffentlichter IP-Liste

Nur hier ist `Verified` überhaupt erreichbar. Alle Quellen liefern das von Google
eingeführte Format `{ "creationTime": …, "prefixes": [ { "ipv4Prefix": … } ] }`.

| Anbieter | Agenten | Quelle | Stand der Quelle | Präfixe |
|---|---|---|---|---|
| Anthropic | ClaudeBot, Claude-User, Claude-SearchBot | <https://claude.com/crawling/bots.json> | 2026-08-18 | 26 |
| OpenAI | GPTBot | <https://openai.com/gptbot.json> | 2025-10-30 | 21 |
| OpenAI | OAI-SearchBot | <https://openai.com/searchbot.json> | 2026-01-02 | 39 |
| OpenAI | ChatGPT-User | <https://openai.com/chatgpt-user.json> | 2026-09-21 | 225 |
| Perplexity | PerplexityBot | <https://www.perplexity.ai/perplexitybot.json> | 2025-02-07 | 8 |
| Perplexity | Perplexity-User | <https://www.perplexity.ai/perplexity-user.json> | 2025-10-17 | 4 |
| Apple | Applebot, Applebot-Extended | <https://search.developer.apple.com/applebot.json> | 2026-09-15 | 24 |
| Google | Google-Extended, Googlebot | <https://developers.google.com/static/crawling/ipranges/common-crawlers.json> | 2026-09-21 | 317 |

Anmerkungen:

- **Anthropic** veröffentlicht eine gemeinsame Liste für die ganze Crawler-Infrastruktur;
  die drei Agenten unterscheiden sich nur im User-Agent. Die frühere Aussage „we do not
  publish IP ranges" gilt nicht mehr.
- **OpenAI** führt je Agent eine eigene Liste. `ChatGPT-User` ist mit Abstand die größte,
  weil dort nutzergetriebene Abrufe aus breit gestreuter Cloud-Infrastruktur kommen.
- **Google-Extended** ist nur ein robots.txt-Token, kein eigener User-Agent. Die Abrufe
  kommen aus den Googlebot-Bereichen, deshalb liegt beides auf derselben Liste.
  Googlebot selbst gilt in LogLens als Suchbot (SPEC 5.6), nicht als KI-Agent; die
  Bereiche stehen trotzdem drin, damit die Liste vollständig bleibt.
- Die frühere Google-Adresse `https://developers.google.com/static/search/apis/ipranges/googlebot.json`
  leitet inzwischen auf `crawling/ipranges/common-crawlers.json` um.

## Anbieter ohne veröffentlichte IP-Liste

Für diese Agenten bleibt das Verdikt `Unverified`, solange die Anfrage kein Angriff ist.
Sie stehen mit leerer Präfixliste und einer Begründung in der JSON-Datei, damit in der
Oberfläche sichtbar wird, dass hier nichts geprüft werden *kann* – statt den Eindruck zu
erwecken, die Prüfung sei fehlgeschlagen.

| Anbieter | Agenten | Mögliche Prüfung | Warum nicht im Browser |
|---|---|---|---|
| Amazon | Amazonbot | Reverse-DNS unter `crawl.amazonbot.amazon` | DNS-Abfragen sind aus WebAssembly heraus nicht möglich und wären ein externer Request |
| Meta | meta-externalagent, meta-externalfetcher, facebookexternalhit | Herkunfts-ASN AS32934 | keine maschinenlesbare Liste; `whois -h whois.radb.net -- '-i origin AS32934'` |
| ByteDance | Bytespider | – | keine veröffentlichte Liste |
| Common Crawl | CCBot | – | keine veröffentlichte Liste |

Weitere Agenten in `ai-agents.json` (MistralAI-User, DeepSeekBot, Cohere-AI, YouBot,
AgentTrustBot) haben ebenfalls keine Bereiche und bleiben damit `Unverified`.

## Liste aktualisieren

Die Quellen einzeln herunterladen:

```bash
mkdir -p /tmp/iprng && cd /tmp/iprng
curl -sS  -o anthropic.json      https://claude.com/crawling/bots.json
curl -sS  -o gptbot.json         https://openai.com/gptbot.json
curl -sS  -o oai-searchbot.json  https://openai.com/searchbot.json
curl -sS  -o chatgpt-user.json   https://openai.com/chatgpt-user.json
curl -sSL -o perplexitybot.json  https://www.perplexity.ai/perplexitybot.json
curl -sSL -o perplexity-user.json https://www.perplexity.ai/perplexity-user.json
curl -sS  -o applebot.json       https://search.developer.apple.com/applebot.json
curl -sSL -o googlebot.json      https://developers.google.com/static/crawling/ipranges/common-crawlers.json
```

Danach in `ai-ip-ranges.json` je Anbieter `prefixes`, `sourceUpdated` und `retrieved`
ersetzen sowie `updated` auf das Abrufdatum setzen. Jeder Eintrag behält Quelle, Agenten
und Prüfverfahren.

Nach der Aktualisierung müssen `dotnet build` und `dotnet test` grün sein. Die Tests in
`tests/LogLens.Core.Tests/Classification/PatternResourceTests.cs` prüfen, dass jeder
Anbieter Quelle, Stand und Agenten trägt und dass Anbieter ohne Liste ihre Begründung
mitbringen.

**Die Fixture-Tests hängen nicht an dieser Datei.** `tests/fixtures/sample-expected.md`
legt fest, dass für die Klassifizierungstests ein eigener Provider mit
`216.73.216.0/22` injiziert wird (siehe `FixtureLog.TestIpRanges`). Eine Aktualisierung
der Quellen darf die Erwartungswerte der Fixture also nicht verschieben.

## Wie oft?

Die Listen ändern sich unterschiedlich schnell: OpenAI und Google aktualisieren teils
täglich, Perplexity seit über einem Jahr nicht. Eine veraltete Liste führt nicht zu
falschen Angriffen, sondern nur zu `Unverified` statt `Verified`. Ein Abgleich pro
Release genügt.
