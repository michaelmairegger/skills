# NuGet-Pakete (`src/Directory.Packages.props`)

- Sollte eine `Directory.Packages.props` vorhanden und  (`ManagePackageVersionsCentrally=true`) sein, dann verwendes das Projekt Central Package Management — alle NuGet-Versionen stehen ausschließlich in `Directory.Packages.props`, nicht in den einzelnen `.csproj`-Dateien und werden auch dort aktualisiert. Ansonsten werden die Pakete in den jeweiligen `.csproj`-Dateien aktualisiert
- Ein Teil der Pakete ist an die .NET-Version gekoppelt. Diese **nicht einzeln** bearbeiten — sie werden automatisch mitgezogen, sobald sich die Version in der Datei `version.json` ändert.
- Sollte die Version in `version.json` nicht den .NET versionen entsprechen und eine andere Versionsnummer beinhalten, so passe diese Datei nicht an, dann suche in den anderen Dateien wo die .NET Version definiert wird und erhöhe sie dort
- Sollten mehrere Pakete beim `<PackageVersion>`-Eintrag eine Variable referenziert haben, so muss die entsprechende Variable auf die neue Version aktualisiert werden und nicht der einzelne Eintrag

## Überprüfung

Mit folenden Befehlen wird überprüft ob die Versionsänderungen erfolgreicht angewendet wurden
- `dotnet restore`: Pakete exisieren und werden aufgelöst
- `dotnet build`: Sollte der Befehl nicht erfolgreich ausgeführt werden so lese [Breaking Changes](breaking-changes.md)

## Tests 
- Tests sollen, sofern nicht anders angegeben, nicht ausgeführt werden.