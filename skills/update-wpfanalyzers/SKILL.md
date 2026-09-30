---
name: update-wpfanalyzers
description: Gleicht beim Aktualisieren von `WpfAnalyzers` die `dotnet_diagnostic.WPF*`-Einträge in den `.editorconfig`-Dateien des Hauptrepos und aller Submodule mit der Upstream-Dokumentation ab. Verwenden, wann immer `WpfAnalyzersVersion` / `MinWpfAnalyzersVersion` oder die `WpfAnalyzers`-Paketversion geändert wird.
---

# WpfAnalyzers aktualisieren

1. Liste der dokumentierten Regeln holen:
   `gh api repos/DotNetAnalyzers/WpfAnalyzers/contents/documentation --jq '.[].name' | sed 's/\.md$//' | sort`
   Das NuGet-`.nuspec` enthält keinen Commit-SHA und neuere Versionen sind nicht getaggt, daher gilt `master` als Referenz.

2. Für jede `.editorconfig` mit `dotnet_diagnostic.WPF`-Einträgen (Hauptrepo + Submodule) die Regel-IDs extrahieren, auch auskommentierte (`;` bzw. `#`), und mit der Liste aus Schritt 1 vergleichen:
   - **Fehlend:** neue Regel als `dotnet_diagnostic.WPFxxxx.severity = error` numerisch sortiert einfügen. Für jede neue Regel kurz die Doku (`documentation/WPFxxxx.md`) lesen und dem Benutzer Titel und Default-Severity nennen.
   - **Duplikate:** entfernen.
   - **Nicht mehr dokumentiert:** nicht löschen, nur melden.
   - Bewusst deaktivierte Regeln (auskommentiert oder `= none`) unverändert lassen. Fehlen sie in einem Submodul komplett, auskommentiert ergänzen wie im Hauptrepo.

3. Submodule mit `WpfAnalyzers`-Paketreferenz, aber ohne WPF-Einträge in `.editorconfig` melden, nicht automatisch befüllen.

4. Solution bauen und neue `WPF*`-Fehler dem Benutzer auflisten, statt sie ungefragt zu beheben oder die Severity herabzusetzen.
