# Vergleichsregeln

Diese Regeln setzt `scripts/compare-config.mjs` um. Bei Änderungen an den Regeln das Skript und diese Datei gemeinsam anpassen.

## Projekte

- Verglichen wird jedes Projekt mit `Sdk="Microsoft.NET.Sdk.Web"` und einer `appsettings.json`, das in mindestens einer Datei einen `Sentry`- oder `Serilog`-Abschnitt hat. Projekte ohne Logging-Konfiguration (z. B. reine Hilfsdienste) fallen heraus.
- Übersprungene Ordner: `node_modules`, `bin`, `obj`, `.git`, `.vs`, `.idea`, `dist`, `artifacts`, `.claude`, `submodule(s)`, `.angular`.
- **Angular-Projekt** = die `.csproj` referenziert `SpaProxy`.
- **Seq-Projekt** = eine der Dateien enthält eine Seq-Senke.

## Dateien

| Tabelle | Inhalt |
|---|---|
| `appsettings.json` | die Datei selbst |
| `appsettings.Production.json` | die Datei, zusammengeführt mit `config.default.json`; Werte daraus bekommen ⚙️ |
| `appsettings.Development.json` | die Datei selbst |

- `Serilog:Using` aus `config.default.json` wird ignoriert.
- JSON mit Kommentaren und nachgestellten Kommas wird wie von `Microsoft.Extensions.Configuration.Json` gelesen. Schlüssel sind case-insensitiv.

## Schlüssel und Werte

- Verglichen werden `$schema`, der `Sentry`- und der `Serilog`-Abschnitt.
- Einträge unter `WriteTo`, `Enrich` und `AuditTo` werden über ihren `Name` zugeordnet, egal ob als Array oder Objekt geschrieben. Ein Eintrag ohne weitere Argumente erscheint als `on`.
- `Serilog:Using` wird pro Assembly als `Serilog:Using:<Name> = on` dargestellt.
- **Nur gesetzt / leer / null:** `Sentry:Dsn`, `*:ApplicationName`, `*:apiKey`, `*:serverUrl`. Werte erscheinen als `(set)`, `""` oder `null`; der eigentliche Wert (DSN, Name, Schlüssel) zählt nicht.
- **App-spezifische Overrides** werden ausgeblendet: Von `Serilog:MinimumLevel:Override:*` bleiben nur Kategorien `Microsoft*` und `System*`, außer benannten HttpClients (`System.Net.Http.HttpClient.<Name>`). Anpassbar mit `--include-override` / `--exclude-override`.

## Tabellen

- Symbole: ✅ gemeinsamer Wert · ➖ nicht gesetzt · 🚫 nicht anwendbar · ⚙️ aus `config.default.json` · Wert in Backticks = abweichend.
- **Gemeinsamer Wert** = der häufigste Wert unter den anwendbaren Projekten, wenn er mindestens zweimal vorkommt.
- **Nicht anwendbar (🚫):**
  - SpaProxy-Schlüssel in Projekten ohne Angular.
  - Seq-Schlüssel (alles mit „seq“) in Projekten ohne Seq.
  - Projekte ohne die jeweilige Datei.
- **Sortierung:** zuerst Schlüssel, die in allen Projekten übereinstimmen; dann solche, die überall übereinstimmen, wo sie anwendbar sind; dann abweichende. Innerhalb jeder Gruppe alphabetisch.
- Immer die ganze Tabelle ausgeben, nie nur geänderte Zeilen.

## Prüfungen

1. `$schema` fehlt (alle Dateien inkl. `config.default.json`). Empfohlen: `https://www.schemastore.org/appsettings.json`. `json.schemastore.org` leitet dorthin weiter. Das Schema kennt Serilog, aber nicht den `Sentry`-Abschnitt.
2. Unbekannte Sentry-Optionen, laut `references/sentry-options.txt`. Sie werden beim Binden stillschweigend ignoriert, typisch z. B. `DiagnosticsLevel` statt `DiagnosticLevel`, `IncludeRequestPayload` oder `EnableTracing`.
3. `WriteTo`/`Enrich`-Objekte ohne `Name`. Laut JSON-Schema ungültig, zur Laufzeit aber gültig, wenn `Name` aus `appsettings.json` kommt.
4. Werte in `appsettings.Production.json`/`appsettings.Development.json`, die `appsettings.json` wiederholen. Gilt für die ganze Datei; `$schema` und Sink-`Name` sind ausgenommen.
5. Abweichungen von `assets/standard.json`. Ein Dateiname als Abschnitt prüft den Wert in genau dieser Datei; `Production`/`Development` prüft den effektiven, zusammengeführten Wert.

## Hintergrund zu den Standardwerten

- Die DSN steht nur in `appsettings.Production.json`. In `appsettings.json` steht `"Dsn": ""`, damit Sentry außerhalb von Production aus ist.
- `Microsoft`/`System: Warning` statt der .NET-Vorlage (`Microsoft.AspNetCore: Warning`): Seit Sentry SDK 7 schickt die Serilog-Senke jeden Log-Eintrag ab Information an Sentry Logs, unabhängig von `EnableLogs`. Mit den Vorlagenwerten landete jede EF-Core-SQL-Abfrage dort.
- In Development steht `Microsoft.EntityFrameworkCore: Information`, damit SQL in der Konsole sichtbar ist. `Microsoft.AspNetCore.SpaProxy: Information` gilt nur für Angular-Projekte.
- Seq: `LevelSwitches:seqSwitch = Verbose` und `controlLevelSwitch: $seqSwitch` ohne `MinimumLevel:ControlledBy`. Der Seq-Server regelt dann nur die Seq-Senke; Sentry bleibt unberührt.
