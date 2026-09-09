# NuGet-Pakete (`src/Directory.Packages.props`)

- Das Projekt verwendet Central Package Management (`ManagePackageVersionsCentrally=true`) — alle NuGet-Versionen stehen ausschließlich in `src/Directory.Packages.props`, nicht in den einzelnen `.csproj`-Dateien.
- Ein Teil der Pakete ist an die .NET-Version gekoppelt. Diese **nicht einzeln** bearbeiten — sie werden automatisch mitgezogen, sobald sich die Version in der Datei `version.json` ändert.
- Alle übrigen Pakete haben eine fest eingetragene Version und müssen manuell aktualisiert werden.
- Vorgehen: veraltete Pakete ermitteln (z. B. `dotnet list package --outdated` passende `<PackageVersion>`-Einträge in `Directory.Packages.props` anheben, danach `dotnet restore` und `dotnet build` ausführen, um Kompatibilität zu prüfen.