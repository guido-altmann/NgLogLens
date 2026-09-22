# LogLens mit Claude Code starten

## 1. Repository vorbereiten

```bash
mkdir loglens && cd loglens
git init
# Inhalt dieses Pakets hineinkopieren:
#   CLAUDE.md
#   docs/SPEC.md
#   docs/reference/prototype_classifier.py
#   tests/fixtures/sample-nginx.log
#   tests/fixtures/sample-expected.md
git add . && git commit -m "Projektgrundlage: Spezifikation, Fixtures, Prototyp"
claude
```

`CLAUDE.md` im Projektstamm liest Claude Code automatisch zu Beginn jeder Sitzung.
Kein `/init` ausführen, sonst wird die vorbereitete Datei überschrieben bzw. vermischt.

Echte Logs gehören **nicht** ins Repo. Zum Ausprobieren eine Kopie außerhalb des Projekts
ablegen oder in einen Ordner, den `.gitignore` ausschließt.

## 2. Erster Prompt (Planung, noch kein Code)

Am besten im Plan-Modus starten (Shift+Tab, bis „plan mode“ angezeigt wird):

```
Lies CLAUDE.md, docs/SPEC.md und tests/fixtures/sample-expected.md vollständig.
Schau dir docs/reference/prototype_classifier.py an, aber beachte die dort dokumentierten
Abweichungen zur Spezifikation.

Erstelle noch keinen Code. Liefere:
1. Deine Zusammenfassung der Architektur in eigenen Worten (max. 15 Sätze)
2. Einen Umsetzungsplan für Meilenstein M1 und M2 mit Projekten, Klassen, Interfaces und Tests
3. Offene Fragen und Stellen, an denen die Spezifikation aus deiner Sicht unklar oder
   widersprüchlich ist, insbesondere zu den Erwartungswerten in sample-expected.md
4. Die NuGet-Pakete, die du einbinden willst, mit Version und Begründung
```

Fragen beantworten, Plan ggf. korrigieren, dann freigeben.

## 3. Prompts je Meilenstein

**M1 + M2**
```
Setze M1 und M2 aus docs/SPEC.md um, wie im Plan besprochen.
Arbeite testgetrieben: zuerst die Parser-Tests gegen tests/fixtures/sample-nginx.log,
dann die Implementierung. Am Ende dotnet build und dotnet test ausführen und das Ergebnis
zeigen. Committe M1 und M2 getrennt.
```

**M3**
```
Setze M3 (Klassifizierung) um. Lege die Pattern-Listen als eingebettetes JSON an.
Recherchiere die aktuell veröffentlichten IP-Bereiche der KI-Anbieter, dokumentiere die
Quellen in docs/ip-range-sources.md und lege Resources/ai-ip-ranges.json an.
Ein Test pro Fixture-Zeile (Theory mit InlineData oder MemberData) prüft Klasse, Kategorie,
Agent, Verdikt, PageView und Monitoring-Flag gegen sample-expected.md.
Falls ein Erwartungswert aus deiner Sicht falsch ist: nicht anpassen, sondern begründet nachfragen.
```

**M4**
```
Setze M4 um: Upload-Seite mit Drag & Drop, Fortschritt und Abbruch sowie die Übersichtsseite.
Achte auf die WASM-Fallstricke aus CLAUDE.md. Schreibe einen bUnit-Test für die
Upload-Komponente. Zeig mir am Ende, wie ich die App lokal starte.
```

**M5, M6, M7** analog, jeweils mit Verweis auf den Abschnitt in SPEC.md.

## 4. Tipps für die Zusammenarbeit

- Pro Meilenstein eine frische Sitzung (`/clear`) hält den Kontext klein. CLAUDE.md und SPEC.md
  liefern das nötige Wissen neu.
- Wenn Claude Code eine Regel falsch umsetzt, die Korrektur in SPEC.md oder CLAUDE.md eintragen
  lassen, nicht nur im Chat. Sonst ist sie in der nächsten Sitzung vergessen.
- Für die Validierung mit dem echten Log: Die Zahlen des Prototyps (4.233 Anfragen,
  3.584 Duplikatzeilen, 138 Seitenaufrufe) sind ein Richtwert. Durch die korrigierten Regeln
  (SPEC 5.4, 5.7, 5.8) weichen sie bewusst leicht ab. Lass dir die Abweichungen erklären.
