---
name: project-versions
description: Listet für alle Projekte (Git-Repositories) unter einem oder mehreren Ordnern die verwendeten .NET-, .NET-Framework-, Angular- und Flutter-Versionen, den Update-Pfad (zuerst neuester Minor/Patch des aktuellen Majors, dann jeder folgende Major ohne Überspringen) und den Support-Status, als farbcodierte Markdown-Tabelle. Verwenden, wann immer nach Versionen, veralteten Frameworks, anstehenden Migrationen, End-of-Life oder Support-Status über mehrere Projekte gefragt wird.
---

# Versionen und Support-Status aller Projekte

Die Tabelle wird von einem Skript erzeugt, nicht von Hand. Es löst MSBuild-Properties auf (`$(NetCoreAppCurrent)`, NBGV `version.json`), liest Lock-Dateien und holt die neuesten Versionen und Support-Daten live. Von Hand gehen dabei leicht Projekte, Ziel-Frameworks oder Patch-Stände verloren.

## Ablauf

1. Skript mit den Ordnern aufrufen, die die Projekte enthalten. Absolute Pfade verwenden, weil `dotnet run` das aktuelle Verzeichnis nicht beibehält:
   ```
   dotnet run <skill-dir>/scripts/project-versions.cs -- E:\code\michaelmairegger.it
   ```
   Optionen:
   - `--soon-days <n>` (Standard 90): ab wann ein Support-Ende als „endet bald“ (⏳/🟨) gilt.
   - `--format json`: maschinenlesbare Ausgabe statt Markdown, siehe [JSON-Ausgabe](#json-ausgabe). Warnungen gehen auf stderr, damit stdout gültiges JSON bleibt.
   Der erste Lauf dauert wegen des Builds länger. Das Skript braucht Internet und das .NET 10 SDK oder neuer.
2. Die Ausgabe **vollständig** weitergeben: Kopfzeile mit den neuesten Versionen, Legende, alle Zeilen und Fußnoten.
3. Danach kurz einordnen: Was ist dringend (🟥, ⏳ mit nahem Datum)? Wo fehlt dem Skript Information (siehe unten)?

## Regeln der Darstellung

Diese Regeln hat der Benutzer festgelegt. Bei Änderungen Skript und diese Liste gemeinsam anpassen.

- **Update-Pfad:**
  - Zuerst `→` auf den neuesten Minor/Patch des **aktuellen** Majors.
  - Danach `⇒` über **jeden** folgenden Major, jeweils auf dessen neuesten Minor/Patch. Keine Version wird übersprungen (z. B. Angular 17 ⇒ 18 ⇒ 19 ⇒ 20 ⇒ 21 ⇒ 22).
- **.NET Framework:** zuerst auf 4.8.1, dann `⇒ Portierung` direkt auf den neuesten .NET-Major. Die Portierung führt nicht über .NET 5, 6 usw.
- **Farbe der Version** (wie `npm outdated`): 🔴 Update im aktuellen Major verfügbar · 🟡 nur ein neuer Major verfügbar · 🟢 aktuell.
- **Spalte Support** je Technologie für die verwendete Version: ✅ unterstützt · ⏳ endet bald (mit Datum) · ❌ ohne Support (seit Datum).
- **Ganze Zeile** nach dem schlechtesten Support-Status: 🟥 / 🟨 / 🟩. Die 🟥-Zeilen kommen zuerst. Im Terminal lassen sich Zeilen nicht echt einfärben, deshalb das Quadrat vor dem Projektnamen.

## Wie das Skript Versionen ermittelt

- **Projekt:** jedes Git-Repository unter dem Ordner. Ignoriert werden `node_modules`, `bin`, `obj`, `build`, `submodule(s)`, Flutter-`ephemeral` und `example`.
- **.NET:**
  - `TargetFramework(s)` und `TargetFrameworkVersion` aus allen `.csproj`.
  - Properties aus `*.props` werden aufgelöst, `_NBGV_Version` stammt aus `version.json`.
  - Den Patch-Stand liefern Properties `*NetVersion` (`CurrentNetVersion`, `PreviousNetVersion`, `MinimumNetVersion`).
  - Ohne solche Properties ist nur das Target Framework bekannt. Die Zeile ist dann 🔴, weil offen ist, ob ein Patch fällig ist.
  - `netstandard` wird ausgelassen, weil es keine Laufzeit ist.
  - Haben mehrere Ziel-Frameworks unterschiedliche Projekte, steht der Projektname davor. Bei reinem Multi-Targeting entfällt er.
- **Angular:** `@angular/core` aus `package-lock.json` (installierte Version). Ohne Lock-Datei gilt die Range aus `package.json`.
- **Flutter:**
  - Gepinnte Version aus `.fvmrc` oder `.fvm/fvm_config.json`, sonst die Mindestversion `flutter: ">=x"` aus `pubspec.lock`.
  - Flutter hat kein LTS, nur das aktuelle Stable wird gepflegt.
  - Ohne Pin zeigt die Tabelle „✅ (Pipeline: stable)“, wenn eine YAML-Pipeline den Channel `stable` verwendet. Sonst steht „✅*“: Der Status gilt dann nur, wenn tatsächlich mit dem aktuellen Stable gebaut wird.
- **Quellen:**
  - .NET: `releases-index.json` von Microsoft
  - .NET Framework und Angular-Support: endoflife.date
  - Angular-Versionen: npm-Registry
  - Flutter: `releases_windows.json`

## JSON-Ausgabe

Markdown und JSON entstehen aus demselben Datenmodell. Emojis und Pfeile gibt es nur im Markdown.

- **Wurzel:** `generatedAt`, `soonDays`, `latest` (neueste Versionen), `projects[]`, `withoutTechnology[]`.
- **Projekt:** `name`, `path`, `dotnet[]`, `angular[]`, `flutter[]`, `support` (schlechtester Status, entspricht der Zeilenfarbe).
- **Eintrag:**
  - `current` mit `currentSource`. Bei `targetFramework` ist der Patch unbekannt, sonst z. B. `version.json`, `CurrentNetVersion`, `package-lock.json`, `pubspec.lock-minimum`.
  - `update`: `minor` 🔴 · `major` 🟡 · `none` 🟢 · `unknown`.
  - `latestInMajor` und `majorPath[]`: der Update-Pfad.
  - .NET zusätzlich `kind` (`core`/`framework`), `targetFramework`, `projects[]` und `portTo` (nur bei .NET Framework).
  - Angular zusätzlich `location` und `range`.
  - Flutter zusätzlich `location` und `pipelineStable`.
- **`support`:**
  - `status`: `supported`, `soon`, `eol` oder `unknown`.
  - `until`: Ende des Supports (ISO-Datum).
  - `lts`: bei .NET der Release-Typ LTS, bei Angular „befindet sich in der LTS-Phase“.
  - `note`: Hinweis, z. B. bei Flutter.
- **Konventionen:** Versionen sind immer Strings. Leere Listen werden als `[]` ausgegeben, fehlende Werte als `null`. So bleibt das Schema stabil.

## Grenzen

- **React/Next und andere Technologien** werden nicht erfasst. Fragt der Benutzer danach, das Skript um eine Spalte erweitern.
- **Branch:** Das Skript liest den ausgecheckten Stand jedes Repositories. Soll ein bestimmter Branch verglichen werden (z. B. `dev`), vorher auschecken und pullen, aber nur nach Rückfrage.
- **Pakete ohne Lock-Datei:** yarn- und pnpm-Lock-Dateien werden nicht gelesen. Dort gilt die Range aus `package.json`.
