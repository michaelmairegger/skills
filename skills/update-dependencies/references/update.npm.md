# npm-Pakete (`src/client/package.json`)

- in `package.json` wenn möglich die minimale Version eintragen, die die gewünschte Funktionalität bietet (z. B. `^34.0.0` statt `^34.0.1`), damit Minor- und Patch-Updates automatisch mitgezogen werden.
- Abhängigkeiten werden über `npm update` im Verzeichnis `src/client` aktualisiert; anschließend `package.json`- und `package-lock.json`-Diff kontrollieren.
- Angular: Bei Major-Versionssprüngen (z. B. 15 → 16) werden über `ng update` aktualisiert (z. B. `npx ng update @angular/core@<Major> @angular/cli@<Major>`), damit Angular seine automatischen Migrationsschritte (Schematics) anwenden kann. Bei Major-Versionssprüngen jede Major-Version einzeln nacheinander durchführen, nicht überspringen. Die Migration wird mit dem `-C` parameter in einem eigenen Commit abgeschlossen, z. B. `ng update @angular/core@<Major> @angular/cli@<Major> -C`.
- wenn `biome` aktualisiert wurde, dan muss biome mit den Argumenten `migrate --write` migriert werden
- Nach jedem Update: `npm install` ausführen (aktualisiert `package-lock.json` vollständig), danach `npm run build`
- Tests sollen vorläufig nicht ausgeführt werden.