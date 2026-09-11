# npm-Pakete (`src/client/package.json`)

- in `package.json` wenn möglich die minimale Version eintragen, die die gewünschte Funktionalität bietet (z. B. `^34.0.0` statt `^34.0.1`), damit Minor- und Patch-Updates automatisch mitgezogen werden.
- Abhängigkeiten werden mit dem Befehl `npm update` im Verzeichnis `src/client` aktualisiert; anschließend `package.json`- und `package-lock.json`-Diff kontrollieren.

## Vorgehen


## Angular updates

- Major-Versionssprung (z.B. 21 → 22)
  - Git Änderungen müssen vorher committed werden
  - Werden mit dem Befehl `ng update` aktualisiert (z. B. `npx ng update @angular/core@<Major> @angular/cli@<Major>`), damit Angular seine automatischen Migrationsschritte (Schematics) anwenden kann.
  - Jede Major-Version einzeln nacheinander durchführen, nicht überspringen. (z.B. 20 → 21 → 22) Die Migration wird mit dem `-C` parameter in einem eigenen Commit abgeschlossen, z. B. `ng update @angular/core@<Major> @angular/cli@<Major> -C`.
- Minor-Versionssprung (z.B. 22.0 → 22.1)
  - Werden mit dem Befehl `npm update` aktualisiert
  - Git Änderungen sollen vorher nicht committed werden

## Überprüfung

- `npm install`: Pakete exisieren und werden aufgelöst (aktualisiert ggf. `package-lock.json` vollständig)
- `npm run build`

## Tests 
- Tests sollen, sofern nicht anders angegeben, nicht ausgeführt werden.

## Nach dem Aktualisieren von Paketen

Wenn folgende Pakete aktalisiert werden, so müssen nachfolgende Schritte ausgeführt weden:

### biome
- Die Konfigurationsdate wir mit `migrate --write` migriert
- Verwende `format --write` umd `.ts` , `.html`, `.scss` Dateien zu formattieren