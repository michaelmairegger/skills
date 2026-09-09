---
name: update-dependencies
description: Beschreibt, wie NuGet- und npm-Pakete aktualisiert werden und wie dabei `version.json` mitzuführen sind. Verwenden, wann immer NuGet-Pakete, npm-Pakete oder die Projekt-/.NET-Versionsnummer aktualisiert werden sollen. VERWENDE DIESEN SKILL NICHT FÜR MIGRATIONEN AUF NEUE MAJOR-VERSIONEN VON .NET
---

# Pakete aktualisieren

## Allgemeine Hinweise

- Rate nicht die neue Versionsnummer: Sie muss der tatsächlich installierten oder veröffentlichten .NET-SDK-Patch-Version entsprechen (z. B. überprüft über `dotnet --version` oder die offizielle .NET-Release-Ankündigung unter `https://dotnet.microsoft.com/en-us/download/dotnet`).
- Verwende diesen Skill bei Änderungen des .NET Patchversion (z. B. `10.0.10` → `10.0.11`). Für größere Migrationsschritte auf neue Major-Versionen (z.B. `.NET 10` → `.NET 11`) verwende den skill `migrate-dotnet{OLD_VERSION}-to-dotnet{NEW_VERSION}` (z.B. `migrate-dotnet10-to-dotnet11` für die Migration von .NET 10 zu .NET 11) welcher im Plugin `dotnet-upgrade@dotnet-agent-skills` verfügbar ist
- Wenn keine .NET-Patchversion verfügbar ist, soll kein Paket aktalisiert werden. Jedoch soll der Nutzer gefragt werden, ob er die Pakete trotzdem aktualisieren möchte.
- Beim Update der Major-Version von Syncfusion Paketen muss ein neuer Lizenzschlüssel angefordert werden. Minor-Versionen können mit dem bestehenden Lizenzschlüssel genutzt werden, daher können diese ohne Lizenzänderung aktualisiert werden.
- Alle Pakete mit demselben Prefix (z. B. `syncfusion`, `sentry`) (NuGet und NPM) müssen, um Kompatibilitätsprobleme zu vermeiden, auf die gleiche Hauptversion aktualisiert werden. Wenn dies nicht möglich ist, muss der Benutzer um Bestätigung gebeten werden, dass die Aktualisierung für diese Pakete trotzdem durchgeführt werden soll.

## Paketaktualisierungen verschiedener Paketmanager


| Paketmanager | Dateiendung | Updateüberprüfung | Weiterführende Schritte |
|---|---|---|---|
|NuGet |`*.csproj`, `*.sln`, `*.slnx`|`dotnet list package --outdated` | [Updateschritte](references/update.nuget.md)
|NPM|`package.json`|`npm outdated`|[Updateschritte](references/update.npm.md)
|NuGet| `*.esproj`| Überprüfe, ob ein neues Sdk, welches im Projekt Knoten mit dem Sdk Attribut angegeben ist, verfügbar ist |Version aktualisieren|

## Major-Versionssprünge

- Wenn ein Major-Versionssprung eines Paketes durchgeführt wird, lese [breaking-changes.md](references/breaking-changes.md)

## Commit-Konventionen für Paket-Updates

- Es gilt grundsätzlich der `commit-messages`-Skill
- Commit-Konvention (etablierte Ausnahme vom sonstigen `type:`-Format): `chore: bump version to <Version>`

## Empfohlene Reihenfolge

1. Verfügbare .NET-Patchversion prüfen → `version.json`
2. Übrige NuGet-Paketversionen in `src/Directory.Packages.props` aktualisieren, mit `dotnet build` verifizieren.
3. `esproj` SDK-Version prüfen und ggf. aktualisieren, mit `dotnet build` verifizieren.
3. npm-Pakete aktualisieren (`npm update` bzw. `ng update` für Angular-Kernpakete), mit `npm run build` verifizieren.
4. Abschließenden Commit erstellen
