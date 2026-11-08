# Biome aktualisieren (`@biomejs/biome`)

Jede Biome-Version bringt neue Lint-Regeln, Assist-Aktionen und Formatter-Optionen mit. Die meisten davon sind nicht automatisch aktiv: Neue Regeln landen zuerst in der Gruppe `nursery`, viele Stil-Regeln sind auch später nicht „recommended“. Damit nützliche Regeln nicht unbemerkt bleiben, wird bei jedem Biome-Update **jede** neue Regel und Option geprüft und dem Benutzer werden die Regeln vorgeschlagen, die die Lesbarkeit verbessern können. Ohne Rückfrage wird nichts aktiviert.

Alle Befehle im Verzeichnis mit der `biome.json`/`biome.jsonc` ausführen (z. B. `src/client`).

## 1. Versionen bestimmen

- Alte Version aus dem Stand vor dem Update, z. B. `git show HEAD:src/client/package-lock.json` → `packages["node_modules/@biomejs/biome"].version`.
- Neue Version: `npx biome --version`.
- Hat sich die Version nicht geändert, entfallen die folgenden Schritte.

## 2. Konfiguration migrieren

- `npx biome migrate --write` passt die Konfigurationsdatei an (umbenannte Regeln und Optionen, verschobene Gruppen, `$schema`-Version).
- Den Diff der Konfigurationsdatei kontrollieren.

## 3. Neue Regeln und Optionen ermitteln

1. Schema der alten Version in den Scratchpad laden, nicht ins Projekt:
   ```
   npm pack @biomejs/biome@<alt> --pack-destination <scratchpad>
   tar xzf <scratchpad>/biomejs-biome-<alt>.tgz -C <scratchpad> package/configuration_schema.json
   ```
2. Schemas vergleichen ([`scripts/biome-schema-diff.mjs`](../scripts/biome-schema-diff.mjs), benötigt nur Node):
   ```
   node <skill>/scripts/biome-schema-diff.mjs --explain <scratchpad>/package/configuration_schema.json node_modules/@biomejs/biome/configuration_schema.json
   ```
   Das Skript listet für `linter.rules` und `assist.actions` die neuen Regeln, die Regeln mit Gruppenwechsel (meist aus `nursery` in eine stabile Gruppe übernommen) und die entfernten bzw. umbenannten Regeln, außerdem neue und entfernte Konfigurationsoptionen (Formatter, Parser, Domains, …). `--explain` ergänzt über `biome explain` je Regel, ob sie empfohlen ist, die Standard-Severity, die Art des Fixes (`safe`, `unsafe`, `keiner`) und die Version, seit der es sie gibt.
3. Den [Changelog](https://raw.githubusercontent.com/biomejs/biome/main/packages/%40biomejs/biome/CHANGELOG.md) für alle Versionen zwischen alt und neu lesen. Das Schema zeigt nur neue Namen. Geänderte Formatter-Defaults, neue Optionen bestehender Regeln und geändertes Regelverhalten stehen nur im Changelog.

## 4. Jede neue Regel und Option bewerten

Alle Einträge aus Schritt 3 durchgehen und einordnen:

| Kategorie | Erkennung | Vorgehen |
|---|---|---|
| Automatisch aktiv | `Empfohlen = ja`, Gruppe ≠ `nursery`, Preset `recommended` (Standard) und Regel nicht in der Konfiguration abgeschaltet | Wird von `biome lint` ohnehin gemeldet. Verstöße in Schritt 6 beheben. In der Liste als „bereits aktiv“ kennzeichnen. |
| Vorschlag: Lesbarkeit | nicht aktiv, verbessert Lesbarkeit oder Konsistenz (siehe unten) | in die Vorschlagsliste aufnehmen |
| Nicht relevant | Framework, Bibliothek oder Sprache wird im Projekt nicht verwendet (z. B. React, Vue, Svelte, Qwik, Solid, Astro, Next, Playwright, Drizzle, GraphQL, YAML) | nicht vorschlagen, nur in der Zusammenfassung zählen |
| Sonstige | z. B. Korrektheit, Sicherheit, Performance | kurz erwähnen, wenn sie für das Projekt nützlich erscheinen |

Ob ein Framework verwendet wird, anhand von `package.json` und der Dateitypen im Projekt prüfen.

Eine Regel oder Option verbessert die Lesbarkeit, wenn sie z. B.:
- eine einheitliche Schreibweise erzwingt (Array-Typen, Funktionsdeklarationen, Imports und Exports, Namenskonventionen, Literale),
- Verschachtelung oder Komplexität reduziert (Early Return, `else` nach `return`, verschachtelte Ternaries, Callback-Tiefe),
- unnötigen Code entfernt (überflüssige Template-Ausdrücke, Bedingungen, Typannotationen, Fragmente),
- eine ausdrucksstärkere API bevorzugt (`some()` statt `findIndex() !== -1`, `Math.min()` statt Ternary, `at()`, `startsWith()`, Template-Literale),
- sortiert oder gruppiert (Imports, Objekt-Schlüssel, Attribute, CSS-Eigenschaften),
- als Formatter-Option die Darstellung vereinheitlicht (Klammern, Kommas, Zeilenumbrüche, Attribut-Positionen).

`nursery`-Regeln sind instabil: Sie können in Patch-Versionen umbenannt, verändert oder entfernt werden und sind auch mit `Empfohlen = ja` nicht aktiv. Sie dürfen vorgeschlagen werden, sind aber als `nursery` zu kennzeichnen.

## 5. Weitere vorhandene Regeln und Optionen ermitteln

Zusätzlich zu den neuen Einträgen wird gezeigt, was es sonst noch gibt und im Projekt noch nicht genutzt wird:

```
node <skill>/scripts/biome-schema-diff.mjs --explain --list node_modules/@biomejs/biome/configuration_schema.json
```

Daraus die Lint-Regeln (v. a. `style`, `complexity`), Assist-Aktionen (`source`, z. B. `useSortedKeys`, `useSortedAttributes`) und Formatter-Optionen auswählen, die
- nicht empfohlen und damit nicht automatisch aktiv sind,
- nicht bereits in der Konfiguration stehen (`npx biome inspect config` zeigt die aufgelöste Konfiguration inkl. `extends` und `overrides`; ältere Versionen ohne `inspect`: Konfigurationsdatei und ihre `extends` lesen),
- nach den Kriterien aus Schritt 4 die Lesbarkeit verbessern und zum Projekt passen.

Regeln, die in der Konfiguration bewusst auf `"off"` stehen, und bewusst gesetzte Formatter-Optionen werden nicht erneut vorgeschlagen.

## 6. Auswirkungen messen

Für alle Kandidaten aus Schritt 4 und 5 die Treffer im Projekt zählen, ohne Dateien zu ändern:

```
npx biome lint --only=<gruppe>/<regel> --only=<gruppe>/<regel> ... --reporter=summary
```

`--only` aktiviert auch abgeschaltete Regeln. Die Zusammenfassung listet die Anzahl der Treffer je Regel. Regeln, die dort nicht erscheinen, haben keine Treffer und können ohne Codeänderung als Schutz aktiviert werden. Für Formatter-Optionen die Option temporär setzen, `npx biome format` ohne `--write` ausführen, die Anzahl geänderter Dateien notieren und die Konfiguration wieder zurücksetzen.

Für die automatisch aktiven Regeln aus Schritt 4 `npx biome lint` ausführen und die Treffer je Regel ebenso festhalten.

## 7. Vorschläge zeigen und nachfragen

Dem Benutzer zwei nummerierte Tabellen zeigen, nach Gruppe sortiert:

1. **Neue Regeln und Optionen dieser Version**, die die Lesbarkeit verbessern können.
2. **Weitere vorhandene Regeln und Optionen**, die noch nicht genutzt werden (aus Schritt 5).

| Nr. | Regel / Option | Gruppe | Empfohlen | Fix | Treffer | Nutzen für die Lesbarkeit | Beispiel |
|---|---|---|---|---|---|---|---|

- „Nutzen“ in einem Satz auf Deutsch beschreiben, nicht die englische Beschreibung kopieren.
- „Beispiel“: kurzes Vorher/Nachher, möglichst aus einem Treffer im Projekt, sonst aus `npx biome explain <regel>`.
- Darunter kurz zusammenfassen: automatisch aktive neue Regeln mit ihren Treffern, die Anzahl nicht relevanter Regeln mit den betroffenen Frameworks bzw. Sprachen und entfernte bzw. umbenannte Regeln.

Danach fragen, welche Nummern übernommen werden sollen. Bei längeren Listen die Nummern als Freitext erfragen, statt Auswahlfragen zu verwenden. Severity (`error`, `warn`, `info`) und ggf. Regel-Optionen nach dem Muster bereits konfigurierter Regeln wählen und bei Unklarheit mitfragen. Optional anbieten, abgelehnte Regeln explizit auf `"off"` zu setzen, damit sie bei künftigen Updates nicht erneut vorgeschlagen werden.

Ohne Zustimmung keine Regel aktivieren und keine Formatter-Option ändern.

## 8. Anwenden

1. Ausgewählte Regeln in der Konfigurationsdatei unter `linter.rules.<gruppe>` eintragen, Assist-Aktionen unter `assist.actions.source`, Formatter-Optionen im passenden Abschnitt (`formatter`, `javascript.formatter`, `css.formatter`, `html.formatter`, …). Einträge innerhalb einer Gruppe alphabetisch einsortieren.
2. `npx biome check --write` wendet sichere Fixes, Assist-Aktionen und die Formatierung an. Dateien `.ts`, `.html`, `.scss` werden mit `npx biome format --write` formatiert.
3. Unsichere Fixes (`Fix = unsafe`) nur nach Rückfrage und je Regel einzeln anwenden: `npx biome lint --only=<gruppe>/<regel> --write --unsafe`. Danach den Diff prüfen.
4. Verbleibende Treffer ohne Fix (Datei:Zeile, Regel) dem Benutzer auflisten. Sie nicht ungefragt mit `biome-ignore` unterdrücken und die Severity nicht ungefragt herabsetzen.
5. Prüfen: `npx biome check` ohne Fehler und `npm run build`.
6. Änderungen durch neu aktivierte Regeln und Formatter-Optionen in einen eigenen Commit nach dem Paketupdate (`style:` bei reiner Formatierung, sonst `refactor:`). So bleibt das Paketupdate reviewbar.
