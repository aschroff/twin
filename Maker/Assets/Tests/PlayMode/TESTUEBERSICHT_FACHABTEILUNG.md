# Automatisierte App-Tests — Übersicht für die Fachabteilung

Dieses Dokument beschreibt die automatisierten Tests, die **die App wie ein Anwender bedienen**:
Sie tippen auf dieselben Schaltflächen, wählen Werkzeuge, malen auf den Körper, legen Twins an
und prüfen anschließend, was die App anzeigt und speichert. Rein technische Tests (einzelne
Programmfunktionen, Schnittstellen zu externen Diensten) sind hier bewusst nicht aufgeführt.

**Ablauf einer Testausführung:** Jeder Test startet die App neu, arbeitet in einem eigenen,
temporären Datenverzeichnis und räumt danach auf. Die eigenen Twins der Testerin oder des Testers
werden dabei nicht verändert. Gestartet werden die Tests aus Unity heraus
(Tools → Template PoC → …); das Ergebnis ist je Test „bestanden" oder „nicht bestanden".

**Stand:** 20. August 2026 — 20 App-Tests, alle bestanden.

---

## 1. Grundlegende Bedienung

### Twin anlegen und wiederfinden
**Prüfziel:** Ein neuer Twin lässt sich anlegen und erscheint in der Twin-Liste.
1. Schaltfläche „Speichern" öffnen — die Twin-Ansicht erscheint mit den Schaltflächen
   „Neu", „Speichern unter", Namensfeld, „Zurücksetzen" und „Export".
2. Namen eingeben und „Neu" wählen.
3. Die App wechselt zurück zur Hauptansicht.
4. Twin-Ansicht erneut öffnen.

**Erwartet:** Der neue Twin steht in der Liste.

### Einstellungen erreichbar
**Prüfziel:** Die Einstellungen lassen sich öffnen.
1. Schaltfläche „Einstellungen" wählen.

**Erwartet:** Die Einstellungsansicht erscheint.

### Vollständiger Bearbeitungsdurchlauf
**Prüfziel:** Der zentrale Arbeitsablauf funktioniert von Anfang bis Ende.
1. Twin öffnen und in den Bearbeitungsmodus wechseln.
2. Prüfen, dass alle Werkzeug-Schaltflächen vorhanden sind.
3. Eine gespeicherte Ansicht (Blickwinkel) wählen.
4. Mit einem Marker eine Markierung auf den Körper malen.
5. Zurück zur Hauptansicht und die Gruppendetailseite öffnen.

**Erwartet:** Die gemalte Markierung ist dort in ihrer Gruppe aufgeführt.

---

## 2. Gruppen und Markierungen

### Markierungen bleiben der richtigen Gruppe zugeordnet
**Prüfziel:** Jede Markierung liegt genau in der Gruppe, in der sie gemalt wurde.
1. Beispiel-Twin „LipEdema" öffnen (enthält die Gruppen Pain, Injuries, Treatment, Swell).
2. In jede Gruppe eine Markierung malen, jeweils mit einem anderen Marker.
3. Eine neue Gruppe anlegen und dort ebenfalls eine Markierung malen.
4. Auf der Gruppendetailseite die Gruppen einzeln auswählen.

**Erwartet:** Zu jeder Gruppe wird genau ihre eine Markierung angezeigt.

### Gruppe aus- und wieder einblenden
**Prüfziel:** Das Ein- und Ausblenden einer Gruppe verändert deren Inhalt nicht.
1. Markierung in eine Gruppe malen.
2. Gruppe ausblenden, danach wieder einblenden.
3. Erneut in dieselbe Gruppe malen.

**Erwartet:** Die vorhandene Markierung bleibt erhalten, und das Malen funktioniert danach
weiterhin.

### Gruppenliste: Anzahl, Anlegen, Löschen
**Prüfziel:** Die Gruppenverwaltung zeigt und ändert die richtigen Daten.
1. Gruppenliste öffnen und die angezeigte Anzahl der Markierungen je Gruppe prüfen.
2. Eine neue Gruppe anlegen, indem der leere Eintrag benannt wird.
3. Eine Gruppe löschen, die nicht die aktuell ausgewählte ist.

**Erwartet:** Die Anzahlen stimmen, die neue Gruppe wird zur aktuellen Gruppe und ein frischer
leerer Eintrag erscheint, die gelöschte Gruppe verschwindet, alle übrigen Daten bleiben.

### Twin verlassen und erneut öffnen
**Prüfziel:** Nichts geht beim Speichern und erneuten Laden verloren.
1. In zwei Gruppen je eine Markierung malen.
2. Eine Gruppe ausblenden, eine Gruppe als aktuelle Gruppe auswählen.
3. Twin verlassen und wieder öffnen.

**Erwartet:** Gruppen, Markierungen, deren Zuordnung, die Sichtbarkeit und die ausgewählte Gruppe
sind unverändert.

---

## 3. Anzeige der Twin-Informationen

Betrifft den Twin-Namen in der Kopfzeile jeder Ansicht sowie den Informationsblock
„Twin / Version / Werkzeug / Gruppe" in der Übersichtsleiste.

### Nach dem Zurücksetzen der App
**Prüfziel:** Nach einem Zurücksetzen zeigen alle Anzeigen den dann geladenen Twin.
1. Twin „LipEdema" öffnen.
2. App zurücksetzen.

**Erwartet:** Kopfzeile und Übersicht zeigen „default", Version „000", und da der leere Twin keine
Gruppen hat, wird keine aktuelle Gruppe angezeigt.

### Beim Öffnen und beim Anlegen eines Twins
**Prüfziel:** Name und Version folgen dem geöffneten bzw. neu angelegten Twin.
1. Einen Twin aus der Liste öffnen — Name und Version werden geprüft.
2. Einen neuen Twin anlegen — Name und Version „000" werden geprüft.

**Erwartet:** Kopfzeile und Übersicht stimmen in beiden Fällen mit dem geladenen Twin überein.

### Werkzeug- und Gruppenanzeige
**Prüfziel:** Die Übersicht zeigt immer das aktive Werkzeug und die aktuelle Gruppe.
1. Im Bearbeitungsmodus einen Marker auswählen, danach einen anderen.
2. Eine Gruppe als aktuelle Gruppe wählen, danach eine andere.

**Erwartet:** Werkzeugname und Gruppenname wechseln jeweils mit.

---

## 4. Export und Import von Twins

Grundlage für den späteren Austausch von Twin-Versionen zwischen zwei Anwendern über einen
Server. Ein Twin wird als ZIP-Datei ausgegeben und kann auf einem anderen Gerät eingelesen
werden.

### Die Bemalung ist nach dem Import sofort sichtbar
**Prüfziel:** Ein importierter Twin sieht aus wie der exportierte — ohne Zusatzschritte.
1. Twin bemalen und exportieren.
2. App zurücksetzen, sodass alle Twins entfernt sind.
3. Die ZIP-Datei importieren und den Twin öffnen.

**Erwartet:** Die Bemalung des Körpers ist unmittelbar sichtbar. Der Test vergleicht dazu die
bemalte Fläche mit der des Originals (Abweichung unter 10 %). Es ist nicht nötig, erst eine
Gruppe aus- und wieder einzublenden.

### Gruppen und Markierungen überstehen den Austausch
**Prüfziel:** Der Import überträgt die vollständige Struktur des Twins.
1. Twin bemalen, exportieren, App zurücksetzen, importieren, öffnen.

**Erwartet:** Alle Gruppen mit ihren Namen, die Markierung und deren Zuordnung sind vorhanden und
weiterverwendbar.

### Vorhandene Twins werden nie überschrieben
**Prüfziel:** Ein Import legt eine neue Version an, statt Bestehendes zu ersetzen.
1. Vorhandenen Twin exportieren.
2. Dieselbe Datei zweimal importieren.

**Erwartet:** Es entstehen die Versionen „V01" und „V02"; der ursprüngliche Twin bleibt unverändert
erhalten. Die Version wird dabei stets in die erste freie Nummer gelegt.

### Der Dateiname der ZIP-Datei ist unerheblich
**Prüfziel:** Der Twin behält seinen eigenen Namen, unabhängig davon, wie die Datei heißt.
1. Twin exportieren und die ZIP-Datei umbenennen (wie es ein Mailprogramm oder ein Server tut).
2. Datei importieren.

**Erwartet:** Der Twin trägt weiterhin seinen Namen aus der eigenen Konfiguration, ist in der
Versionsliste seines Namens zu finden und lässt sich öffnen.

### Rückkehr eines bereits importierten Twins
**Prüfziel:** Ein Twin, der zwischen zwei Anwendern hin- und hergeht, bleibt eindeutig.
1. Twin exportieren, importieren (ergibt „V01").
2. Diese Version exportieren und erneut importieren.

**Erwartet:** Die mitgebrachte Versionsbezeichnung bleibt erhalten, die neue Kennung wird
angehängt („000V01V01"). Hinweis: Die Bezeichnung wächst mit jedem Austausch — siehe offene
Punkte.

### Import, während derselbe Twin geöffnet ist
**Prüfziel:** Der geöffnete Twin und der importierte bleiben getrennt.
1. Twin öffnen, eine Markierung malen, exportieren.
2. Eine zweite Markierung malen — nur der geöffnete Twin hat nun zwei.
3. Die exportierte Datei importieren.
4. Beide Versionen nacheinander öffnen.

**Erwartet:** Der importierte Twin hat eine Markierung, der geöffnete zwei. Keiner der beiden
überschreibt den anderen.

### Der geöffnete Twin ist in der Liste erkennbar
**Prüfziel:** Die Twin-Liste weist stets den Twin aus, den die App gerade anzeigt.
1. Twin öffnen, exportieren, importieren (ergibt eine zweite Version).
2. Twin-Liste ansehen.

**Erwartet:** Die Zeile zeigt die geöffnete Version und ist als geöffnet gekennzeichnet. Die
importierte Version ist über die Versionsübersicht hinter dieser Zeile erreichbar.

### Beschädigte Datei
**Prüfziel:** Ein fehlerhafter Import richtet keinen Schaden an.
1. Eine unbrauchbare Datei mit dem Namen eines vorhandenen Twins importieren.

**Erwartet:** Es entsteht kein Twin, und der vorhandene Twin mit diesem Namen bleibt vollständig
erhalten.

---

## 5. Sticker

Sticker-Bilder gehören zum jeweiligen Twin. Mehrere Twins können denselben Sticker-Platz mit
unterschiedlichen Bildern belegen.

### Das Sticker-Bild folgt dem geöffneten Twin
**Prüfziel:** Es wird immer das Bild des geöffneten Twins verwendet.
1. Zwei Twins anlegen und denselben Sticker-Platz mit einem roten bzw. blauen Bild belegen.
2. Zwischen den Twins hin- und herwechseln.

**Erwartet:** Werkzeug und Bemalung verwenden jeweils das Bild des geöffneten Twins — nie das des
zuvor geöffneten.

### Ein importierter Twin bringt seine Sticker mit
**Prüfziel:** Alle Sticker-Bilder eines Twins werden mit ausgetauscht.
1. Twin mit zwei Sticker-Bildern exportieren.
2. Einen anderen Twin öffnen, der dieselben zwei Sticker-Plätze mit anderen Bildern belegt.
3. Den exportierten Twin importieren und öffnen.

**Erwartet:** Beide Bilder sind mit dem Twin übertragen worden und werden verwendet.

---

## Bekannte offene Punkte

Diese Punkte sind bekannt und nicht durch Tests abgedeckt, weil sie fachlich zu entscheiden sind:

- **Versionsbezeichnung wächst beim Austausch.** Geht ein Twin mehrfach zwischen Anwendern hin und
  her, entstehen Bezeichnungen wie „000V01V01". Fachlich zu klären, sobald Versionen als
  Zeitstempel geführt werden.
- **Auffindbarkeit importierter Versionen.** Die Twin-Liste zeigt eine Zeile pro Twin-Name; weitere
  Versionen liegen in der Versionsübersicht. Kommen Twins künftig unbemerkt über einen Server an,
  ist zu klären, wie der Anwender darauf aufmerksam wird.
- **Twin-Namen sind auf 11 Zeichen begrenzt**, die Fehlermeldung nennt jedoch 14.
- **Der aktuell geöffnete Twin kann nicht gelöscht werden**, dadurch lässt sich die Twin-Liste über
  die App nicht vollständig leeren.
- **Dateigröße des Exports:** Jeder Export enthält die Körperbemalung als Bilddatei (etwa 1,2 MB).
  Exporte aus älteren App-Versionen enthalten diese nicht und kommen unbemalt an.

---

## Nicht in dieser Übersicht

Technische Tests ohne fachlichen Ablauf: der Dienst „Text → Markierung" (Bemalen einer
Körperregion aus einer Beschreibung), das programmgesteuerte Bemalen als Grundlage des
Vorlagengenerators, sowie die Tests der Schnittstelle zum Sprachmodell (OpenAI), die einen
gültigen Zugangsschlüssel benötigen. Die technische Gesamtübersicht steht in `TESTS_OVERVIEW.md`.
