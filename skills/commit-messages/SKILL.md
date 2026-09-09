---
name: commit-messages
description: Konventionen für commit-messages. Verwenden, wann immer ein `git commit` für dieses Repository erstellt wird.
---

# Commit-Messages

Diese Konventionen wurden aus der bestehenden Commit-Historie dieses Repositories abgeleitet (`git log`). Sie gelten für jeden Commit in diesem Projekt.

## Format der Betreffzeile

```
<type>: <kurze, prägnante Beschreibung>
```

- Folge Conventional Commits https://raw.githubusercontent.com/conventional-commits/conventionalcommits.org/refs/heads/master/content/v1.0.0/index.md
- **Kein Punkt** am Ende der Betreffzeile.
- **Kein Scope** in Klammern (z. B. nicht `feat(grid): ...`) — in diesem Projekt wird durchgängig ohne Scope committet.
- Die Beschreibung ist eine kurze, sachliche Zusammenfassung der Änderung (üblicherweise ca. 5–15 Wörter), im Präsens/Imperativ ("add", "fix", "update", "enhance", "validate" statt "added"/"fixed").

## Erlaubte Types

| Type       | Verwendung                                                                                                  |
|------------|-------------------------------------------------------------------------------------------------------------|
| build      | Changes that affect the build system or external dependencies (example scopes: gulp, broccoli, npm)         |
| ci         | Changes to our CI configuration files and scripts (example scopes: Travis, Circle, BrowserStack, SauceLabs) |
| docs       | Documentation only changes                                                                                  |
| feat       | A new feature                                                                                               |
| fix        | A bug fix                                                                                                   |
| perf       | A code change that improves performance                                                                     |
| refactor   | A code change that neither fixes a bug nor adds a feature                                                   |
| style      | Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)      |
| test       | Adding missing tests or correcting existing tests                                                           |

Wenn eine Änderung mehrere Aspekte abdeckt (z. B. neues Feld **und** UI-Anpassung), reicht ein `feat:`-Commit mit einer Beschreibung, die die wichtigsten Teile aufzählt (durch Kommas getrennt), so wie in:

## YouTrack-Ticket referenz (optional)

Wenn der Commit zu einem bekannten YouTrack-Ticket gehört, wird nach einer Leerzeile eine Referenzzeile ergänzt:

```
#<ProjectPrefix>-<Ticketnummer> fixed <Version>
```

Bei mehreren Tickets:

```
(#<ProjectPrefix>-<Nummer1>, #<ProjectPrefix>-<Nummer2>) fixed <Version>
```

In YouTrack prüfen ob das Feld `Build Version` die aktuelle Kalenderwoche im Format `YY.WW` als auswählbare Option enthält. Wenn nicht, soll der Benutzer darauf hingewiesen werden, dass er das Feld in YouTrack anlegen soll. Danach kann die Version in der Commit-Message ergänzt werden. Dazu wird foldender Text in die Commit-Message eingefügt:

```
build <YY.WW>
```

- Die Ticketnummer (`<ProjectPrefix>-<Ticketnummer>`) stammt aus YouTrack.
- `<Version>` ist die aktuelle Version aus `version.json` im Repo-Root.
- Diese Zeile **nur ergänzen, wenn eine konkrete Ticketnummer bekannt ist** (z. B. vom User genannt oder aus dem Kontext ersichtlich). Ohne bekannte Ticketnummer wird die Zeile weggelassen — sie wird nicht erfunden oder geraten.