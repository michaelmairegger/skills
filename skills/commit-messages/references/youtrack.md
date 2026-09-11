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