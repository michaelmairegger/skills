---
name: update-agp-kotlin
description: Aktualisiert Android Gradle Plugin (AGP), Kotlin Gradle Plugin (KGP) und ggf. den Gradle Wrapper der Flutter-App (src/mobile) auf die neuesten Versionen, die von Flutter und Kotlin unterstützt werden. Verwenden, wann immer AGP, Kotlin oder Gradle der Android-App aktualisiert werden sollen.
---

# AGP / Kotlin / Gradle der Flutter-App aktualisieren

## Dateien

* `src/mobile/android/settings.gradle.kts` – Versionen der Plugins `com.android.application` (AGP) und `org.jetbrains.kotlin.android` (KGP)
* `src/mobile/android/gradle/wrapper/gradle-wrapper.properties` – Gradle-Version (`distributionUrl`)
* `src/mobile/android/local.properties` – Pfad zum Flutter SDK (`flutter.sdk`)

## 1. Flutter-Grenzen ermitteln

* `flutter --version` ausführen.
* In `<flutter.sdk>/packages/flutter_tools/lib/src/android/gradle_utils.dart` folgende Konstanten lesen:
  * `maxKnownAndSupportedAgpVersion` → höchste AGP-Version (major.minor), die Flutter unterstützt
  * `maxKnownAndSupportedKgpVersion` → höchste KGP-Version, die das Tooling kennt
  * `maxKnownAndSupportedGradleVersion`
* In `<flutter.sdk>/packages/flutter_tools/gradle/src/main/kotlin/DependencyVersionChecker.kt` die Mindestversionen lesen (`warn*Version` / `error*Version`).

## 2. Kotlin-Kompatibilität prüfen

Die Tabelle unter https://kotlinlang.org/docs/gradle-configure-project.html#apply-the-plugin zeigt für jede KGP-Version die minimale und maximale Gradle- und AGP-Version.

## 3. Verfügbare Versionen ermitteln

* AGP: https://dl.google.com/android/maven2/com/android/tools/build/gradle/maven-metadata.xml
* KGP: https://repo1.maven.org/maven2/org/jetbrains/kotlin/kotlin-gradle-plugin/maven-metadata.xml

Nur stabile Versionen verwenden (keine alpha-, beta- oder RC-Versionen).

## 4. Versionen auswählen

* **AGP**: neueste stabile Patch-Version innerhalb von `maxKnownAndSupportedAgpVersion` (major.minor). Neuere AGP-Minor-Versionen nicht verwenden, auch wenn Kotlin sie bereits unterstützt.
* **KGP**: neueste stabile Version, deren AGP- und Gradle-Bereich laut Kotlin-Tabelle die gewählte AGP- und Gradle-Version enthält. Eine Version über `maxKnownAndSupportedKgpVersion` ist erlaubt, weil das nur eine Trace-Meldung in `flutter analyze --suggestions` auslöst. Die Kotlin-Tabelle hat Vorrang.
* **Gradle Wrapper**: nur ändern, wenn AGP eine höhere Mindestversion verlangt. Dann die kleinste Version wählen, die ≥ der AGP-Mindestversion ist und im KGP-Bereich liegt.

## 5. Änderung durchführen

* Versionen in `settings.gradle.kts` anpassen, bei Bedarf auch `distributionUrl` im Wrapper.
* Flags in `gradle.properties` (z. B. `android.builtInKotlin`, `android.newDsl`) **nicht** ändern. Diese verwaltet der Flutter-Migrator.

## 6. Verifikation

Im Verzeichnis `src/mobile`:

* `flutter build apk --debug` muss ohne AGP/KGP-Versionswarnungen bauen.
* `flutter analyze --suggestions` darf keine blockierenden Kompatibilitätsprobleme melden.

## 7. Ergebnis berichten

Für AGP, KGP und Gradle jeweils alt → neu angeben. Wenn eine Komponente nicht angehoben wurde, den Grund nennen (z. B. Flutter-Grenze).