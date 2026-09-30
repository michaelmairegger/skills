---
name: youtrack-link-sentry-issue
description: Verlinks ein Sentry Issue mit einem YouTrack Ticket und setzt das Feld `Affected Version` auf die Sentry-Release-Version des ersten Auftretens eines Sentry-Issues. Verwenden, wann immer ein Sentry-Issue in YouTrack referenziert wird oder ein Sentry-Issue in ein YouTrack-Ticket überführt werden soll. Die Version des ersten Auftretens sollt bekannt sein.
---

# Behavior

Ein Sentry-Release hat das Format `<prefix>@major.minor.patch.build` (z.b. `Prefix@10.0.12.1`). In YouTrack wird als `Affected Version` nur `major.minor.patch` (z.b. `10.0.12`) eingetragen.

`Release Prefix`, `Sentry-Issue-Id` und `YouTrack-Ticket-Id` dürfen nicht geraten werden. Wenn unbekannt, muss der Benutzer danach gefragt werden.

## Ablauf

1. Mit dem Skill `sentry-issue-versions` die Releases des Sentry-Issues ermitteln und das `firstRelease` (Release des ersten Auftretens) verwenden. Passt dessen Prefix nicht zum angegebenen `Release Prefix`, stattdessen die niedrigste Version (numerisch) unter den Releases mit diesem Prefix verwenden.
2. Version auf `major.minor.patch` kürzen.
3. Mit `get_issue_fields_schema` (Projekt-Key aus der YouTrack-Ticket-Id, z.b. `PROJ` aus `PROJ-123`) prüfen, ob die Version als Wert von `Affected Version` existiert. Existiert sie nicht, den Benutzer fragen statt einen Wert zu erfinden.
4. Feld setzen: `update_issue` mit `issueId: <YouTrack-Ticket-Id>` und `customFields: { "Affected Version": "<major.minor.patch>" }`. Ist das Feld mehrwertig, bestehende Werte beibehalten und die Version ergänzen (`["<bestehend>", "<major.minor.patch>"]`).
5. Gegenseitige Verlinkung
  - YouTrack
    Sentry-Link zum Ticket als mit `add_comment` und `Link zu [Sentry-Ticket](https://<host>/organizations/<organization>/issues/<Sentry-Issue-Id>)` hinzufügen.
  - Sentry
    Über das Sentry-MCP-Tool `add_issue_note` einen Kommentar im Sentry-Issue anlegen:
    - issueId: Sentry-Issue-Id (z. B. `PROJECT-1AB`)
    - text: `YouTrack: [<YouTrack-Ticket-Id>](https://host.youtrack.cloud/issue/<YouTrack-Ticket-Id>)`
    - Mit dem YouTrack-MCP-Tool `get_issue` prüfen, ob die Beschreibung des Tickets schon auf das Sentry-Issue verweist. Falls nicht, den Verweis mit `update_issue` ergänzen:
   `Sentry: [<Sentry-Issue-Id>](https://<sentry-host>/organizations/<organization>/issues/<Sentry-Issue-Id>)`
