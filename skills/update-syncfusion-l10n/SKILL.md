---
name: update-syncfusion-l10n
description: Gleicht beim Aktualisieren von Syncfusion-Paketen die dazugehörigen Lokalisierungsdateien (WPF `Syncfusion.*.resx` bzw. EJ2/Angular `L10n`-JSON) mit den Upstream-Dateien von Syncfusion ab und ergänzt fehlende Übersetzungen. Verwenden, wann immer die Version von Syncfusion NuGet- oder npm-Paketen geändert wird.
---

# Syncfusion-Lokalisierungsdateien aktualisieren

Syncfusion liefert nur die Standardtexte (Englisch) aus. Neue oder geänderte Schlüssel einer neuen Version fehlen daher in den eigenen Übersetzungen und erscheinen zur Laufzeit auf Englisch. Dieser Skill gleicht die eigenen Dateien mit der neuen Upstream-Version ab.

## 1. Betroffene Dateien finden

| Plattform | Paketquelle | Eigene Dateien (typisch) | Upstream |
|---|---|---|---|
| WPF | NuGet `Syncfusion.*.WPF` | `Syncfusion.<Assembly>.resx` (neutral) und `Syncfusion.<Assembly>.<culture>.resx`, meist in einem `Resources`-Ordner | `syncfusion/wpf-controls-localization-resx-files`, Ordner `<Assembly>/<Assembly>.resx` |
| EJ2 / Angular | npm `@syncfusion/ej2-*` | JSON-Dateien, die per `L10n.load(...)` geladen werden (z. B. `de.json`), oder eine Abhängigkeit auf `@syncfusion/ej2-locale` | `syncfusion/ej2-locale`, `src/<culture>.json` |

- Dateien im Hauptrepo und in allen Submodulen suchen (`git ls-files` / `git submodule foreach`).
- Wird `@syncfusion/ej2-locale` direkt als npm-Paket verwendet, gibt es nichts abzugleichen – nur sicherstellen, dass es mitaktualisiert wird.
- Nur Assemblies/Komponenten abgleichen, für die bereits Lokalisierungsdateien existieren. Pakete, die neu referenziert, aber nicht lokalisiert sind, nur melden.

## 2. Upstream-Referenz bestimmen

Die Zielversion ist die tatsächlich installierte Syncfusion-Version (z. B. aus `Directory.Packages.props` bzw. `package.json`), nicht geraten.

**WPF:** Tags heißen `tags/<Version>`, aber nicht jede Version ist getaggt. Commit-Messages enthalten die Version (`v35.1.37`).
```
gh api repos/syncfusion/wpf-controls-localization-resx-files/tags --jq '.[].name'
gh api "repos/syncfusion/wpf-controls-localization-resx-files/commits?per_page=50" --jq '.[] | .sha[0:8] + " " + (.commit.message|split("\n")[0])'
```
Den jüngsten Commit zur Zielversion verwenden. Gibt es keinen exakt passenden, den letzten Commit mit einer Version ≤ Zielversion verwenden und dem Benutzer mitteilen, welche Version als Referenz diente.

**EJ2:** `ej2-locale` ist nicht sinnvoll getaggt, daher gilt `master` als Referenz.

Datei abrufen:
```
gh api "repos/<owner>/<repo>/contents/<pfad>?ref=<sha>" -H "Accept: application/vnd.github.raw"
```
Heruntergeladene Dateien im Scratchpad ablegen, nicht im Projekt.

## 3. Abgleichen

Schlüssel aus der Upstream-Datei mit der eigenen neutralen Datei bzw. der eigenen Übersetzung vergleichen:

- **Neue Schlüssel:** in die neutrale `.resx` mit dem englischen Upstream-Wert übernehmen und in jeder Kulturdatei (`.<culture>.resx` bzw. `<culture>.json`) eine Übersetzung ergänzen. Terminologie an bereits vorhandene Übersetzungen derselben Datei anpassen (z. B. „Filter“ vs. „Filtern“, Du/Sie-Form). Platzhalter (`{0}`), Tastenkürzel (`&`) und HTML-Tags unverändert lassen.
- **Geänderte englische Werte:** neutralen Wert aktualisieren. Die bestehende Übersetzung prüfen und nur anpassen, wenn sich die Bedeutung geändert hat.
- **Entfernte Schlüssel:** nicht löschen, nur melden – sie schaden nicht und können in älteren Assemblies noch verwendet werden.
- **Eigene Schlüssel, die es upstream nie gab:** unverändert lassen.
- Reihenfolge und Formatierung der bestehenden Datei beibehalten (neue Einträge an passender Stelle bzw. am Ende), damit der Diff klein bleibt. Bei `.resx` das bestehende `<data name="..." xml:space="preserve"><value>...</value></data>`-Format übernehmen.

Bei JSON-Dateien liegen die Schlüssel verschachtelt unter `<culture>.<komponente>.<schlüssel>`; nur die Komponenten abgleichen, die im Projekt verwendet werden.

## 4. Abschluss

1. Build ausführen (`dotnet build` bzw. `npm run build`), um ungültige `.resx`/JSON zu erkennen.
2. Dem Benutzer zusammenfassen:
   - verwendete Upstream-Referenz (Version/SHA),
   - pro Datei: neu übersetzte Schlüssel (Schlüssel, Englisch, Übersetzung), geänderte Werte, upstream entfernte Schlüssel,
   - nicht lokalisierte, aber neu verwendete Syncfusion-Assemblies/Komponenten.
3. Übersetzungen sind maschinell erstellt – den Benutzer auf die Liste zur Durchsicht hinweisen.
4. Die Änderungen gehören in denselben Commit wie das Syncfusion-Paketupdate (siehe Skill `update-dependencies`).
