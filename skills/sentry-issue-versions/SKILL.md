---
name: sentry-issue-versions
description: Listet für ein Sentry-Issue (anhand der Ticket Id) die betroffenen Release-Versionen sowie den Zeitpunkt der Erstellung (First Seen) auf. Verwenden, wann immer nach betroffenen Versionen oder dem Erstellungsdatum eines Sentry-Issues gefragt wird.
---

# Behavior

Zugriff über den Sentry MCP-Server.

Die `Sentry Ticket Id` (Short-Id wie `PROJECT-1AB` oder Issue-URL) darf nicht geraten werden. Wenn unbekannt, muss der Benutzer danach gefragt werden.

## Ablauf

1. Issue-Details abrufen: `get_sentry_resource` mit `resourceType: issue`, `organizationSlug: <organization Slug>`, `resourceId: <Ticket Id>` (bzw. `url`, wenn eine URL angegeben wurde). Daraus lesen:
   - `firstSeen` → Erstellungszeitpunkt des Tickets
   - `lastSeen`
   - `firstRelease` / `lastRelease`
2. Alle betroffenen Releases ermitteln: `execute_sentry_tool` mit `name: get_issue_tag_values` und `arguments: { organizationSlug: <organization Slug>, issueId: <Ticket Id>, tagKey: release }`, inkl. Anzahl Events pro Release.
3. Releases haben das Format `<prefix>@major.minor.patch.build` (z.b. `Project@10.0.11.89`). Nach Version absteigend sortieren (numerisch, nicht lexikographisch).

## Ausgabe

```
|                 |                      |
|-----------------|----------------------|
| Issue:          | <Short-Id> – <Titel> |
| Erstellt        | <firstSeen>          |
| Zuletzt         | <lastSeen>           |
| Erstes Release  | <firstRelease>       |
| Letztes Release | <lastRelease>        |
```

Falls vom Nutzer explizitgewünscht gib tabellarisch noch alle betroffenen Releases mit an

```
| Release            | Version | Events |
|--------------------|---------|-------:|
| Project@10.0.11.89 | 10.0.12 | 42     |
```

Hat das Issue keinen `release`-Tag, dies explizit angeben statt Versionen zu erfinden.
