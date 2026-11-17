# Flutter 

* Bei Flutter Versionsänderungen Release Notes durchlesen (z.B. `https://flutter.dev/blog/whats-new-in-flutter-3-50`) und an den aktuellen Code anpassen.
* Wenn ein Ticketing System bekannt ist:
    * Bestehende Tickets älterer Flutter / Dart Versionen überprüfen, ob die offenen Punkte erledigt werden können. Sind alle Migrationsschritte abgeschlossen, so soll das Ticket unter Anwendung des /commit-messages Skills geschlossen werden
    * Ein Ticket der neuen Flutter / Dart Version anlegen, wo jede Punkte mit einer ToDo Liste abgehakt werden.
* Sämtliche offene Migrationsschritte auch älterer Flutter Verisionen in einer Tabelle ausgeben

z.B.:

|Flutter Version|Dart Version|Titel|Beschreibung|Umsetzbarkeit|
|---|---|---|---|---|
|3.50|3.14|Decoupled design packages|...|1-100|

# pub-Pakete (`pubspec.yaml`)

* Change logs analysieren (z.B. https://pub.dev/packages/<package-name>/changelog) und auf Änderungen welche von der aktuellen Codebasis beinflusst werden hinweisen.
* Bei Breaking changes lese [Breaking Changes](breaking-changes.md)
* Überprüfen ob im Ticketing System offene Tickets zu Flutter / Dart Migrationen existieren, welche Migrationsschritte beinhalten, welche aufgrund der alten Paketversion zurückgestellt worden sind. Dann soll versucht werden, ob mit der neuen Paketversion der Migrationsschritt durchgeführt werden kann.