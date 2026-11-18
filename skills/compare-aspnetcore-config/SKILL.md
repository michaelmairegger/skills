---
name: compare-aspnetcore-config
description: Vergleicht die Sentry- und Serilog-Konfiguration (appsettings.json, appsettings.Production.json, appsettings.Development.json, config.default.json) über mehrere ASP.NET-Core-Projekte, zeigt sie als Tabelle pro Datei und prüft ungültige Sentry-Optionen, fehlende JSON-Schemas, Sinks ohne Name, redundante Werte und Abweichungen vom vereinbarten Standard. Verwenden, wenn Logging-/Sentry-Konfigurationen zwischen Projekten verglichen oder vereinheitlicht werden sollen, oder nach einem Update von Sentry oder Serilog.
---

# ASP.NET-Core-Konfiguration vergleichen

Der Vergleich ist deterministisch und läuft über ein Skript. Die Tabellen nicht selbst aus den Dateien zusammenbauen: Die Regeln (Zusammenführen, Maskieren, „nicht anwendbar“, Sortierung) sind zu zahlreich, um sie zuverlässig von Hand anzuwenden.

## Ablauf

1. Skript mit den Ordnern aufrufen, die die Projekte enthalten. Mehrere Ordner sind erlaubt:
   ```
   node <skill-dir>/scripts/compare-config.mjs E:\code\michaelmairegger.it E:\code\schwer.bz\SchwerVerwaltung
   ```
   Optionen: `--standard <datei|none>`, `--include-override <regex>`, `--exclude-override <regex>`, `--no-checks`.
2. Die Ausgabe (Markdown) **vollständig** an den Benutzer weitergeben: alle Tabellen mit allen Zeilen, dann die Prüfungen. Nicht kürzen und nicht nur Änderungen zeigen, außer der Benutzer verlangt es ausdrücklich.
3. Die Befunde aus **Prüfungen** kurz einordnen: was zur Laufzeit wirkt und was nur das Editor-Schema betrifft. Änderungen an Konfigurationsdateien nur nach Rückfrage vornehmen.
4. Nach Änderungen das Skript erneut ausführen und die vollständigen Tabellen zeigen.

## Laufzeitprüfung mit .NET

Der Vergleich oben bildet die .NET-Regeln nach. Ob eine Konfiguration zur Laufzeit wirklich funktioniert, prüft `scripts/validate-config.cs` mit dem echten `Microsoft.Extensions.Configuration`. Ausführen nach Änderungen an Senken, Level-Switches oder Sentry-Optionen sowie nach einem Update von Sentry oder Serilog:
```
dotnet run <skill-dir>/scripts/validate-config.cs -- E:\code\michaelmairegger.it E:\code\schwer.bz\SchwerVerwaltung
```
- **Absolute Pfade angeben:** `dotnet run` behält das aktuelle Verzeichnis des Aufrufers nicht bei. Der erste Lauf dauert wegen Restore und Build länger.
- **Was geprüft wird**, pro Projekt für Production (inkl. `config.default.json`) und Development:
  - Serilog-Logger mit `ReadFrom.Configuration` bauen; Ausnahmen und `SelfLog`-Meldungen werden gemeldet (unbekannte Senke, falsches Argument, fehlender Level-Switch).
  - `Sentry`-Abschnitt mit `ErrorOnUnknownConfiguration` binden; unbekannte Schlüssel und falsche Werttypen werden gemeldet.
- **Paketversionen:** Die Versionen im Kopf des Skripts sollen zu den Projekten passen. Nutzt ein Projekt eine Senke, die dort nicht referenziert ist, das `#:package` ergänzen, sonst erscheint sie als „unbekannt“.

## Regeln anpassen

- Die Vergleichsregeln stehen in [references/rules.md](references/rules.md). Wünscht der Benutzer eine andere Darstellung, zum Beispiel eine weitere Datei oder einen weiteren maskierten Schlüssel, Skript und `rules.md` gemeinsam ändern.
- Der vereinbarte Sollzustand steht in [assets/standard.json](assets/standard.json). Ändert der Benutzer bewusst einen Standardwert in allen Projekten, auch `standard.json` anpassen.
- Gültige Sentry-Optionen stehen in [references/sentry-options.txt](references/sentry-options.txt). Nach einem Update des Sentry SDK in `scripts/sentry-options.cs` die Paketversion anpassen und die Liste neu erzeugen:
  ```
  dotnet run <skill-dir>/scripts/sentry-options.cs > <skill-dir>/references/sentry-options.txt
  ```

## Fallstricke aus der Praxis

- Die Serilog-Senke von Sentry 7 schickt Logs ab `Information` an Sentry Logs. `EnableLogs` schaltet nur die `ILogger`-Integration und ist für Serilog-Projekte überflüssig.
- `IncludeRequestPayload` gibt es nicht mehr. Ersatz ist `MaxRequestBodySize` (Standard `none`).
- Ein leeres Array für `TraceIgnoreStatusCodes` in appsettings leert die Standardliste **nicht**. Dafür `[0]` verwenden.
- Ein `WriteTo`-Eintrag ohne `Name` in einer Umgebungsdatei funktioniert zur Laufzeit, gilt aber laut JSON-Schema als ungültig. `Name` daher wiederholen.
