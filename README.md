# Toolbox X – v0.6.4

*(vormals "PC-Toolbox" – gleiches Projekt, neuer Name: eine Zukunfts-Toolsammlung,
nicht nur ein PC-Check)*

Dein Schweizer Taschenmesser für PC-Diagnose & Reparatur.
Modular, schnell, quell-einsehbar – kein Abo, keine Spyware.

> **⚠️ Hinweis beim ersten Start:** Windows zeigt beim Öffnen des Setups eine
> SmartScreen-Warnung („Der Computer wurde durch Windows geschützt", Herausgeber
> „Unbekannt"). Das liegt daran, dass das Programm noch **nicht digital
> signiert** ist (ein Code-Signing-Zertifikat kostet Geld und steht noch aus) –
> **nicht** daran, dass etwas Schädliches drin wäre. Zum Fortfahren: „Weitere
> Informationen" → „Trotzdem ausführen".

## Philosophie

> **Nicht konsumieren – erschaffen.**
> **Nicht täuschen – aufdecken.**
> **Reparieren > Wegwerfen.**

Die Toolbox erklärt *warum* etwas kaputt ist und *wie* man es fixen kann –
nicht nur "das ist rot, kauf neu". Kein Feature-Anhäufen ohne Aufräumen
(Negativ-Beispiel: Windows selbst) – erst recherchieren, wo bestehende Tools
(Uninstall Tool, BestSync, Total Commander, ...) Nutzer wirklich im Stich
lassen, dann schlank und gezielt das eigene, bessere Modul bauen. Jede
Diagnose/jeder Fund erklärt zusätzlich konkret **was jetzt zu tun ist** –
nicht nur die reine Information (Vorbild: RetroTrax' eingebautes Hilfe-Prinzip).

## Status (Stand 28.07.2026)

**Öffentlich veröffentlicht: v0.6.4.** Die Free-Version ist modulseitig komplett
und durchgetestet – **19 Module**, jedes im laufenden Programm auf echter
Hardware mit echten Daten gesehen (inkl. Laufwerke/Temperatur mit Administrator-
Rechten: echte SMART-Werte und Sensor-Temperaturen bestätigt, „Windows
reparieren" an einem echten Befund erfolgreich verifiziert). Installer (MSI)
liegt unter [Releases](../../releases) bereit.

**335 automatisierte Tests grün, Build: 0 Fehler / 0 Warnungen.** Die reine
Logik jedes Moduls (Parsing, Bewertung, Sicherheitsregeln) ist in
`src/PC-ToolboxCore` ausgelagert, einem plattformneutralen Projekt ohne WPF-
Abhängigkeit – dadurch laufen die Tests auch ohne echtes Windows.

Es gibt außerdem eine **kostenpflichtige Pro-Version** für Werkstatt-, IT-Profi-
und Systemadministrator-Einsatz (Ordnergröße-Analyse, WLAN-Details, PDF-
Testberichte, u.a.) – deren Code ist nicht Teil dieses Repositories, siehe
[Lizenz](#lizenz).

**Optik-Runde (26.07.) – umgesetzt:**
- **Alex' eigenes Toolbox-X-Logo** eingebaut: als Programm-/Fenster-Icon (echte
  Multi-Size-`.ico`, 16–256 px), im Hero-Banner der Übersicht und in der Titelleiste
- **Eigene Schriften** eingebettet (Chakra Petch, Orbitron, Rajdhani – alle SIL Open
  Font License, frei auch kommerziell). Fließtext/Tabellen bleiben bewusst Segoe UI:
  kondensierte Schriften verschlechtern die Lesbarkeit dichter Listen
- **Ampel-Symbole sind jetzt eigene Vektorgrafik** statt Emoji – dadurch unabhängig
  von der Emoji-Schriftart des jeweiligen Windows und auf jeder Auflösung scharf.
  Gelb ist ein Warndreieck, damit die Stufen auch ohne Farbe unterscheidbar sind
- **Dezente Bewegung**: Kacheln/Buttons skalieren sanft und leuchten auf,
  Sidebar-Balken schiebt sich ein
- **„Woher weiß die Toolbox das?"** an 14 Modulen: nennt per Tooltip die konkrete
  Windows-Quelle (WMI-Klasse, Event-Log-Provider, Registry-Pfad, externes Tool) –
  Transparenz statt Blackbox, inklusive der ehrlichen Grenzen
- **Neue Entstehungs-Seite**: wer woran gebaut hat (Alex / Claude / Hermes)

**V0.01 (Dashboard/Laufwerke/Bluescreen/Temperatur):** auf echter
Windows-Hardware getestet, läuft.

**V0.02 (Super-Uninstaller):** bewusst vorsichtig gebaut:
- **3-Phasen-Sicherheit ist jetzt echter Code** (`Safety/SnapshotManager.cs` +
  `RollbackEngine.cs` + `SafetyEnforcer.cs`), nicht mehr nur Plan im README.
  Registry-Schlüssel werden vor jeder Änderung per `reg export` gesichert,
  Ordner werden beim Entfernen VERSCHOBEN statt gelöscht – beides über
  `RollbackEngine` rückholbar. Generisch gehalten, für künftige schreibende
  Module wiederverwendbar.
- **Leftover-Suche bewusst konservativ** (`Services/LeftoverScanner.cs`):
  keine Tiefensuche durch die ganze Registry/Festplatte, geschützte
  Systempfade (Windows, System32, Common Files, ProgramData\Microsoft, ...)
  sind komplett tabu, jeder Fund ist nur ein Vorschlag mit Erklärung – nichts
  wird automatisch gelöscht, jeder Leftover muss einzeln bestätigt werden.
  Bekannte Grenze dokumentiert: manche Hersteller (Google, Adobe) teilen sich
  einen Registry-Schlüssel für mehrere Produkte – wird als Warnhinweis
  mitgeliefert, nicht automatisch erkannt.
- Vor dem ersten echten Windows-Test wird empfohlen, die deinstallierenden
  Aktionen zuerst in einer **Windows Sandbox** (Wegwerf-VM, in Windows 10/11
  Pro eingebaut) auszuprobieren statt direkt auf dem Hauptsystem.

**V0.03 (BIOS-Kompendium):** reines Nachschlagewerk
(`Services/BiosKompendiumService.cs`), keine Live-Abfrage der echten
BIOS-Einstellungen (Windows kann das nicht auslesen) – nur die eigenen
BIOS-Kenndaten (Hersteller/Version/Datum) werden per WMI angezeigt. 9 sorgfältig
geschriebene Einträge zu den wichtigsten BIOS/UEFI-Einstellungen (Boot-Reihenfolge,
Secure Boot, CSM/UEFI, XMP/DOCP/EXPO, TPM, Fast Boot, Virtualisierung, BIOS-Update,
CMOS-Reset), jeweils mit Ampel-Risikoeinstufung + vier Punkten: was es ist, was bei
Änderung passiert, wann ändern, was tun falls ein Problem auftritt. Bewusst so
geschrieben, dass jeder Eintrag **für sich allein reicht** – ohne Vorwissen, ohne
dass jemand zusätzlich fragen muss (Alex' persönliches Anliegen: viele Nutzer haben
niemanden, den sie fragen können).

**V0.03 (System-Saubermacher):**
Bereinigt 4 bewusst schlank ausgewählte, sichere Kategorien (`Services/CleanupScanner.cs` +
`CleanupService.cs`): temporäre Dateien, Miniaturansichten-Cache, Windows-Update-
Downloadcache, alte Windows.old-Installation. Browser-Caches u.ä. bewusst NICHT
dabei (V1-Scope, kein Feature-Anhäufen).
- **Sicherheit anders gelöst als beim Super-Uninstaller, mit Absicht:** hier geht
  es um echten Festplattenplatz - ein eigener Snapshot-Ordner würde das Ziel
  unterlaufen (nichts wäre freigegeben). Stattdessen übernimmt der **Windows-
  Papierkorb** die Sicherheitsrolle: `Safety/RecycleBinDeleter.cs` verschiebt
  jede Datei/jeden Ordner per klassischer Shell-API (`SHFileOperation`,
  `FOF_ALLOWUNDO`) dorthin statt sie endgültig zu löschen - der Nutzer kann
  jederzeit selbst wiederherstellen. Gesperrte/gerade genutzte Dateien werden
  einzeln übersprungen statt das ganze Modul abstürzen zu lassen.
- Jede Kategorie zeigt vorher Größe + Elementanzahl und braucht eine explizite
  Bestätigung pro Kategorie - kein "alles auf einmal"-Knopf.
- Windows.old ist bewusst Rot markiert (kein Zurück mehr zur alten Windows-
  Version danach) und teilweise systemgeschützt - falls Dateien übersprungen
  werden, verweist die Erklärung auf Windows' eigene Datenträgerbereinigung
  als Fallback, statt stillschweigend nichts zu tun.

## Fernwartung / Remote Desktop

Die Toolbox baut **bewusst keine eigene Remote-Control-Funktion** – RustDesk kann
das schon besser, als wir es selbst nachbauen könnten, und ein selbstgebautes
Remote-Feature würde die Toolbox auch nicht mehr zu 100% "unser eigenes Werk"
machen (fremder Netzwerk-/Krypto-Code). Wer Fernzugriff braucht, nutzt stattdessen
**[RustDesk](https://rustdesk.com/)** — kostenlos, quelloffen, eigener Server möglich
(Alex betreibt bereits einen eigenen RustDesk-Server für sich und Freunde, läuft
stabil auf dem Raspberry Pi). Keine Zeitlimits, keine Kommerziell-Erkennung, keine
Daten über fremde Server.

## 3-Phasen-Sicherheitsprinzip 🔒 (seit V0.02 echter Code, nicht nur Plan)

Jede schreibende Aktion läuft über `Safety/SafetyEnforcer.cs` und folgt diesem Prinzip:

```
PHASE 1: SICHERN 📸
  → Registry-Schlüssel per "reg export" gesichert, Ordner werden VERSCHOBEN statt gelöscht
  → %ProgramData%\PC-Toolbox\Restore\Aktion_DATUM\
  → NIE löschen ohne vorher zu sichern (RollbackEngine kann alles zurückholen)

PHASE 2: AUSFÜHREN ⚡
  → Eigentliche Aktion (z.B. nativer Deinstaller des Herstellers)

PHASE 3: VERIFIZIEREN ✅❌
  → Automatischer Check: Sind Rückstände (Registry/Ordner) übrig geblieben?
  → Jeder Fund ist nur ein VORSCHLAG, kein Autolöschen - Nutzer bestätigt einzeln
```

Noch nicht umgesetzt (spätere Ausbaustufe): echter Windows-Systemwiederherstellungspunkt
zusätzlich zum eigenen Snapshot-Ordner; automatisches Rollback bei Phase-2-Fehlschlag
(aktuell: Fehler wird gemeldet, Snapshot bleibt für manuelles Zurückspielen liegen).

## Was enthalten ist (V0.04/V0.05 – die sechs Module vom 26.07.)

Jedes davon ist bewusst so gebaut, dass man **nichts kaputt machen kann**:

- **Autostart**: alles, was beim Einschalten mitstartet – aus allen vier Orten, an denen
  Windows das versteckt (Registry-Run-Schlüssel, Autostart-Ordner, Aufgabenplanung,
  StartupApproved-Blob). **Ausschalten löscht nichts**, der Eintrag bleibt vollständig
  erhalten und wird nur stillgelegt – genau wie es der Windows-Task-Manager macht,
  also jederzeit mit einem Klick rückholbar.
- **Dienste**: welche Hintergrunddienste dauerhaft mitlaufen, ohne dass sie es müssten.
  **Systemkritische Dienste sind gar nicht erst änderbar**, und empfohlen wird immer
  „nur bei Bedarf" statt „abschalten" – dann startet der Dienst weiterhin, falls ihn
  doch jemand braucht. Ehrlich bleibt es auch: bei DiagTrack steht ausdrücklich dabei,
  dass der PC davon **nicht** spürbar schneller wird (das behaupten nur Tuning-Tools).
- **Netzwerk-Diagnose**: prüft die Kette vom PC bis ins Internet Schritt für Schritt.
  **Nur die erste Bruchstelle wird rot**, Folgefehler gelb – sonst leuchtet alles rot
  und man weiß trotzdem nicht, wo es hakt. Es wird nur gemessen, nichts verändert.
- **Treiber-Check**: findet Geräte, bei denen Windows ein Problem meldet, und erklärt
  22 Geräte-Fehlernummern im Klartext samt Gegenmaßnahme. **Bewusst ohne
  Treiber-Download** – nur lesen, nichts installieren.
- **Commander**: zwei Ordner nebeneinander. **Überschreibt nie**, und Löschen geht nur
  in den Papierkorb.
- **Datenträger (Stufe 1)**: formatieren, Bezeichnung und Laufwerksbuchstabe ändern,
  Größe verlustfrei anpassen. **Das Systemlaufwerk ist nicht anwählbar**, und
  Formatieren erfordert das Abtippen der Bezeichnung.
- **GPU-Diagnose**: VRAM-Test herstellerunabhängig (NVIDIA/AMD/Intel) über die
  mitgelieferte `memtest_vulkan`-Binary. Grün heißt ausdrücklich „keine Fehler im
  getesteten Zeitraum", nicht „garantiert heil".
- **RAM-Disk**: zusätzliches Laufwerk direkt im Arbeitsspeicher (Motor: ImDisk).
- **Hardware-Info** (im System-Modul): Speicherriegel, Laufwerke inkl. DVD/Blu-ray,
  Hersteller-Links.

## Was enthalten ist (V0.02 – Super-Uninstaller)

- **Super-Uninstaller**: Liste aller installierten Programme (Registry-Uninstall-Keys,
  HKLM + WOW6432Node + HKCU), Ampel-Warnung bei systemnahen Komponenten (Laufzeiten/Treiber),
  startet den nativen Deinstaller des Herstellers über die 3-Phasen-Sicherheit, sucht danach
  konservativ nach Registry-/Ordner-Resten und erklärt bei jedem Fund, was er ist und was zu
  tun ist. Rückstände werden einzeln entfernt (nie pauschal "alles löschen"), landen dabei im
  Snapshot-Ordner statt im digitalen Nichts.

## Was enthalten ist (V0.01 – reine Diagnose, nichts Schreibendes)

- **Dashboard**: CPU/RAM/Windows-Version/Mainboard/GPU via WMI (`Win32_OperatingSystem`,
  `Win32_Processor`, `Win32_BaseBoard`, `Win32_VideoController`)
- **Laufwerke**: Speicherplatz (`Win32_LogicalDisk`) + SMART-Rohwerte
  (`root\WMI` → `MSStorageDriver_FailurePredictStatus`/`FailurePredictData`,
  braucht Administrator-Rechte). Modellname und SMART-Werte werden über die
  **echte WMI-Verknüpfung** dem richtigen Laufwerk zugeordnet
  (`Win32_DiskDriveToDiskPartition` → `Win32_LogicalDiskToPartition`, SMART-Instanzname
  ↔ `PNPDeviceID` über `SmartInstanzZuordnung`) – nicht der Reihenfolge nach. Laufwerke
  ohne SMART sagen das im Klartext, statt nur ein Fragezeichen zu zeigen.
- **Bluescreen**: liest **nicht** roh aus `.dmp` (das bräuchte DbgHelp/MiniDumpReadDumpStream –
  zu aufwändig für V0.01), sondern wertet das Windows-Ereignisprotokoll aus
  (Provider `Microsoft-Windows-WER-SystemErrorReporting`, Event-ID 1001). Fallback:
  Zeitstempel-Liste der `.dmp`-Dateien, falls das Event-Log leer ist.
- **Temperatur**: CPU/Board über ACPI-Thermalzone (`MSAcpi_ThermalZoneTemperature`).
  **Achtung:** Diese WMI-Klasse liefert auf vielen modernen Mainboards keine oder
  unplausible Werte – das Modul zeigt dann ehrlich "nicht verfügbar" statt einen Wert
  zu erfinden. **Inzwischen gelöst** für echte Sensorwerte: LibreHardwareMonitorLib ist
  angebunden, braucht aber den PawnIO-Treiber. Der liegt bei (`vendor/pawnio/`) und wird
  **nur auf ausdrücklichen Klick** installiert ("PawnIO installieren" im Temperatur-Modul),
  nie still im Hintergrund.

## Module

Alle 19 Free-Module, jedes im laufenden Programm auf echter Hardware geprüft:

Dashboard · Laufwerke (SMART) · Temperatur · Bluescreen-Analyse · GPU-Diagnose
(VRAM-Test) · RAM-Disk · Autostart · Dienste-Manager · Datenträger (formatieren,
Bezeichnung, Buchstabe, Größe verlustfrei) · Netzwerk-Diagnose · Treiber-Check ·
Commander (Zwei-Spalten-Dateimanager) · Super-Uninstaller (Snapshot +
Leftover-Suche) · BIOS-Kompendium · System-Saubermacher · Windows reparieren
(DISM + sfc) · Registry-Kompendium · Drucker · USB-Geräte

Setup-Programm (MSI, WiX v5) `setup/` + `build-setup.ps1`, Installation in
Windows Sandbox geprüft (s. unten).

**Später (Vision, noch nicht gebaut):** Backup, Sync, Maus-Test, tiefere
Hardware-Diagnose – siehe `HARDWARE_VISION.md`.

**Absichtlich NICHT gebaut:** eigene Fernwartung (siehe unten, RustDesk kann das besser)
und Treiber-Download (der Treiber-Check liest nur, installiert nichts).

## Technik

- **Sprache:** C# .NET 8 WPF
- **UI:** Sidebar links, Inhalt rechts, Statuszeile unten; eigene Titelleiste
  (`WindowChrome`), dunkles Neon-Theme "Toolbox X" + helles Gegenstück, 6 Akzentfarben
  frei wählbar (Einstellungen, gespeichert unter `%AppData%\ToolboxX\settings.json`)
- **Startzeit:** < 1 Sekunde
- **Ampel-System:** einheitlich über alle Module, seit 26.07. als eigene Vektorgrafik
  (`Converters/AmpelIconFactory.cs`) statt Emoji – gleiche Optik auf jedem Windows,
  scharf auf jeder Auflösung, Gelb als Warndreieck (ohne Farbe unterscheidbar)
- **Schriften:** `src/PC-Toolbox/Fonts/` – Chakra Petch (Überschriften), Orbitron
  (Tagline), Rajdhani (Sidebar/Buttons). Alle **SIL Open Font License**
  (`Fonts/*/OFL.txt`), frei auch kommerziell, keine Namensnennungspflicht, als
  Resource in die EXE eingebettet – Nutzer müssen nichts installieren
- **Logo:** `src/PC-Toolbox/Assets/` – Alex' eigenes Toolbox-X-Logo, plus daraus
  zugeschnittenes quadratisches Zeichen und eine echte Multi-Size-`.ico` (16–256 px)
- **Mitgelieferte Fremd-Binaries** liegen bewusst in `vendor/` (nicht unter `src/`),
  jeweils unverändert mit Lizenz und Herkunftsvermerk: `memtest_vulkan` (zlib, VRAM-Test),
  `imdisk` (GPL-2.0, RAM-Disk), `pawnio` (GPL-2.0, echte Temperatursensoren). Die zwei
  echten **Treiber** (ImDisk, PawnIO) werden **nie still installiert**, sondern nur auf
  ausdrücklichen Klick mit normalem Installer-Dialog und UAC

## Modul-Vorbilder (Alex' Referenz-Tools, seit Jahren genutzt)

Als Vorbild empfohlen — aber bewusst nicht 1:1 nachgebaut, sondern erst
recherchiert wo Nutzer an diesen Tools scheitern, dann schlanker/besser gebaut:
- **Uninstall Tool 3.8.1** → Vorbild für Super-Uninstaller (V0.02, gebaut)
- **Total Commander Ultimate Prime** → Vorbild für den Commander (V0.05, gebaut)
- **BestSync** → Vorbild für Backup & Sync-Modul (noch nicht gebaut)

## Build & Test

```powershell
cd PC-Toolbox
dotnet build -c Debug
dotnet run --project src/PC-Toolbox
```

Kompilieren geht auch auf dem Pi (Cross-Compile via `EnableWindowsTargeting` in
der `.csproj`), nur *ausführen* braucht echtes Windows.

**Echte automatisierte Tests laufen dagegen auch auf dem Pi:**
`dotnet test PC-Toolbox.sln` führt `src/PC-Toolbox.Tests` wirklich aus (nicht nur
Compile-Check) – aktuell **335 Tests**. Möglich, weil die reine Logik
(Deinstall-Befehl parsen, Namens-Abgleich für Leftover-Suche, Pfad-Sicherheitsregeln,
Autostart-/Dienste-Bewertung, Aufgaben-XML, Gerätefehler-Codes) in
**`src/PC-ToolboxCore`** ausgelagert ist – ein plattformneutrales `net8.0`-Projekt
ohne WPF/Registry-Abhängigkeit. `PC-Toolbox` (WPF, `net8.0-windows`) selbst kann
auf Linux weiterhin nur kompiliert, nicht ausgeführt werden – dafür braucht es
echtes Windows (seit 26.07. steht Alex' PC dafür bereit) oder eine Windows Sandbox/VM.

**Stolperstein, den es hier schon gab:** Tests, die mehrzeilige Text-Ausschnitte per
`Replace` austauschen, dürfen sich nicht auf `\n` verlassen – je nachdem, ob die Datei
mit LF (Pi) oder CRLF (Windows) ausgecheckt ist, trifft das Muster nicht zu und
`Replace` gibt den String **stillschweigend unverändert** zurück. Zwei Tests waren
deshalb auf dem Pi grün und auf Windows rot, obwohl der Code korrekt war. Lösung:
Zeilenenden im Testmaterial per `ReplaceLineEndings("\n")` normalisieren.

**Auf einigen Modulen braucht Windows Administrator-Rechte**, sonst bleiben sie leer
statt zu lügen: Laufwerke (SMART über `root\WMI`) und Temperatur (ACPI-Thermalzone).

## Setup-Programm bauen

```powershell
dotnet tool install --global wix --version 5.0.2   # einmalig
.\build-setup.ps1
```

Ergebnis: `setup\bin\ToolboxX-Setup.msi` (~64 MB).

Das Skript macht drei Schritte: Release-Publish, Kontrolle des Publish-Ordners,
MSI-Build. Die Version wird aus der `.csproj` gelesen, steht also nur an einer Stelle.

**Warum self-contained (und damit so groß)?** Ohne self-contained müsste der Nutzer
erst die .NET-8-Desktop-Runtime installieren – und genau das ist für die Zielgruppe
("Leute, die niemanden zum Fragen haben") die Hürde, an der es scheitert. Ein größerer
Download ist der bessere Handel als ein Setup, das mit einer Runtime-Fehlermeldung
abbricht.

**Warum die Kontrolle in Schritt 2 drinsteht:** Beim ersten Versuch fehlten die drei
mitgelieferten Fremd-EXEs im Publish – der Single-File-Bundler packt `.exe`-Dateien
ein, statt sie daneben zu legen (behoben per `ExcludeFromSingleFile` in der `.csproj`).
Das fällt sonst **nicht** auf: das Programm startet normal, nur VRAM-Test, RAM-Disk und
die PawnIO-Installation funktionieren still nicht mehr. Deshalb prüft das Skript alle
11 Pflichtdateien und bricht ab, statt ein kaputtes MSI zu bauen.

**Bewusste Entscheidungen im Installer** (Details als Kommentar in `setup/ToolboxX.wxs`):
kein Windows-Dienst, kein Autostart-Eintrag (Toolbox X ist ein Werkzeug, das man
startet – genau das kritisiert das Autostart-Modul an anderer Software), keine stille
Treiber-Installation, abwählbare Desktop-Verknüpfung, und die Snapshot-Sicherungen
unter `%ProgramData%\PC-Toolbox\Restore` bleiben beim Deinstallieren liegen (das sind
Nutzerdaten).

**Installation in Windows Sandbox geprüft (27.07.2026)** – alle Punkte bestanden:
Installation ohne Fehler, Zielordner `C:\Program Files\Toolbox X`, alle 11 Dateien da,
Startmenü-Eintrag vorhanden, Desktop-Symbol korrekt **nicht** angelegt (Feature war
abgewählt), Eintrag unter Programme und Features korrekt, **kein Dienst und kein
Autostart-Eintrag**, Programm startet aus dem Installationsordner, und die
Deinstallation entfernt Ordner samt Verknüpfung restlos. Das Testgerüst liegt nicht im
Repo (Wegwerf-Skript), lässt sich aber in zehn Minuten nachbauen: `.wsb`-Datei mit
gemapptem Ordner + `LogonCommand`, das ein PowerShell-Skript startet und einen Bericht
in den gemappten Ordner schreibt.

**Noch nicht signiert:** Das MSI hat keine Code-Signatur. Windows zeigt beim Öffnen
deshalb eine SmartScreen-Warnung („Der Computer wurde durch Windows geschützt"), und
der Herausgeber steht als „Unbekannt" (s. Hinweis ganz oben). Ein
Code-Signing-Zertifikat (kostenpflichtig, jährlich) steht noch aus.

## Lizenz

Siehe [`LICENSE.md`](LICENSE.md): kostenlos nutzbar und unverändert weitergebbar,
aber keine klassische Open-Source-Lizenz (OSI) – Änderungen/abgeleitete Werke
sind nicht erlaubt, alle Rechte bleiben beim Urheber. Der Quellcode liegt hier
zu Transparenzzwecken offen (siehe die "Entstehung"-Seite in der App).

Mitgelieferte Fremd-Werkzeuge behalten ihre eigenen Lizenzen (zlib bzw. GPL-2.0),
eingebettete Schriften stehen unter der SIL Open Font License.

Es gibt eine kostenpflichtige **Pro-Version** für Werkstatt-/IT-Profi-Einsatz;
deren Code ist nicht Teil dieses Repositories.

## Unterstützen

Toolbox X bleibt kostenlos und werbefrei, ohne Abo und ohne versteckte Kosten – kein
Zwang, nur wer mag. Wenn dir das Programm hilft, kannst du einen Kaffee spendieren:

[![ko-fi](https://storage.ko-fi.com/cdn/kofi3.png?v=3)](https://ko-fi.com/mukkemann)
