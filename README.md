# Toolbox X – v0.6.23

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

## Status (Stand 19.08.2026)

**Öffentlich veröffentlicht: v0.6.23.** Die Free-Version umfasst **24 Module**,
jedes im laufenden Programm auf echter Hardware mit echten Daten geprüft.
Installer (MSI) liegt unter [Releases](../../releases) bereit.

**538 automatisierte Tests grün (Free-Konfiguration), Build: 0 Fehler / 0
Warnungen.** Die reine Logik jedes Moduls (Parsing, Bewertung,
Sicherheitsregeln) ist in `src/PC-ToolboxCore` ausgelagert, einem
plattformneutralen Projekt ohne WPF-Abhängigkeit – dadurch laufen die Tests
auch ohne echtes Windows (u.a. auf einem Raspberry Pi im Team-Sandbox-Setup).

Es gibt außerdem eine **kostenpflichtige Pro-Version** für Werkstatt-, IT-Profi-
und Systemadministrator-Einsatz (Duplikat-Finder, Software-Depot, Windows-
Update-Profil, Festplatten-Klonen, Datenvernichter, HDD-Health-Monitor, u.v.a.)
– deren Code ist nicht Teil dieses Repositories, siehe [Lizenz](#lizenz).

## Module

Alle 24 Free-Module, jedes im laufenden Programm auf echter Hardware geprüft:

- **System** – CPU/RAM/Windows-Version/Mainboard/GPU im Überblick, plus
  Live-Systemmonitor (CPU/RAM/Disk/Netzwerk in Echtzeit, Verlauf als CSV
  exportierbar)
- **Laufwerke** – Speicherplatz + echte SMART-Werte, dem richtigen Laufwerk
  korrekt zugeordnet (nicht der Reihenfolge nach)
- **Temperatur** – CPU/GPU/Board-Sensoren (LibreHardwareMonitorLib/PawnIO,
  nur auf Klick installiert)
- **GPU-Diagnose** – VRAM-Test herstellerunabhängig (NVIDIA/AMD/Intel)
- **RAM-Disk** – zusätzliches Laufwerk direkt im Arbeitsspeicher (Motor: ImDisk)
- **Autostart** – alles, was beim Einschalten mitstartet, aus allen vier
  Orten, an denen Windows das versteckt – Ausschalten löscht nichts
- **Dienste** – welche Hintergrunddienste dauerhaft mitlaufen, mit ehrlicher
  Einschätzung statt Tuning-Tool-Versprechen
- **Akku-Gesundheit** – aktuelle Kapazität vs. Werkszustand (Laptops)
- **Datenträger** – formatieren, Bezeichnung/Laufwerksbuchstabe ändern, Größe
  verlustfrei anpassen – Systemlaufwerk ist nicht anwählbar
- **Netzwerk** – prüft die Kette PC → Router → Internet Schritt für Schritt,
  plus gezielte Einzeladress-Diagnose (DNS/Hosts/Proxy/TCP-Connect)
- **Treiber-Check** – findet Geräte mit Windows-Fehlermeldung, erklärt die
  Fehlernummern im Klartext, lädt aber nichts herunter
- **Commander** – zwei Ordner nebeneinander, mit Suche/Filter, Tastaturkürzeln
  und ZIP-Browsing – überschreibt nie, löscht nur in den Papierkorb
- **Drucker** – Status statt kryptischem Symbol, samt IPP-Reparatur-Hilfe
- **USB-Geräte** – angeschlossene Geräte inkl. Live-Überwachung beim
  An-/Abstecken (auch für generische, namenlose Geräte)
- **Maus-Diagnose** – Status & Brücke zu MausWerk
- **Security-Audit** – verständliche Übersicht sicherheitsrelevanter
  Windows-Einstellungen
- **Bluescreen-Analyse** – wertet Minidumps und den Zuverlässigkeitsverlauf aus
- **Uninstaller** – Deinstallation über die native Routine des Herstellers
  plus konservative Leftover-Suche, alles über die 3-Phasen-Sicherheit
- **Datenrettung** – gelöschte Dateien finden
- **BIOS-Kompendium** – die wichtigsten BIOS/UEFI-Einstellungen erklärt,
  ohne dass Windows sie live auslesen könnte
- **Saubermacher** – vier bewusst sichere Kategorien Speicherplatz freigeben,
  über den Windows-Papierkorb rückholbar
- **Windows reparieren** – DISM + sfc, mit Windows-Update-Verlauf
- **Registry-Kompendium** – wichtige Registry-Stellen erklärt statt blind
  editierbar
- **Windows-Bordmittel** – versteckte klassische Systemwerkzeuge (Geräte-
  Manager, msconfig, Firewall mit erweiterter Sicherheit, u.a.) direkt
  auffindbar und erklärt

Setup-Programm (MSI, WiX v5) `setup/` + `build-setup.ps1`, Installation in
Windows Sandbox geprüft (s. unten).

**Später (Vision, noch nicht gebaut):** tiefere Hardware-Diagnose, eigene
Hardware-Adapter-Anbindung.

**Absichtlich NICHT gebaut:** eigene Fernwartung (siehe unten, RustDesk kann
das besser) und Treiber-Download (der Treiber-Check liest nur, installiert
nichts).

## Fernwartung / Remote Desktop

Die Toolbox baut **bewusst keine eigene Remote-Control-Funktion** – RustDesk kann
das schon besser, als wir es selbst nachbauen könnten, und ein selbstgebautes
Remote-Feature würde die Toolbox auch nicht mehr zu 100% "unser eigenes Werk"
machen (fremder Netzwerk-/Krypto-Code). Wer Fernzugriff braucht, nutzt stattdessen
**[RustDesk](https://rustdesk.com/)** — kostenlos, quelloffen, eigener Server möglich
(Alex betreibt bereits einen eigenen RustDesk-Server für sich und Freunde, läuft
stabil auf dem Raspberry Pi). Keine Zeitlimits, keine Kommerziell-Erkennung, keine
Daten über fremde Server.

## 3-Phasen-Sicherheitsprinzip 🔒

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

Rein lesende Aktionen (die meisten Diagnose-Module) verändern grundsätzlich
nichts am System. Wo der Windows-Papierkorb die passendere Sicherheitsrolle
übernimmt (z.B. Saubermacher, Commander), wird das explizit so gebaut statt
eigener Snapshots – Details stehen jeweils im Modul selbst.

## Technik

- **Sprache:** C# .NET 8 WPF
- **UI:** Sidebar links, Inhalt rechts, Statuszeile unten; eigene Titelleiste
  (`WindowChrome`), dunkles Neon-Theme "Toolbox X" + helles Gegenstück, 6 Akzentfarben
  frei wählbar (Einstellungen, gespeichert unter `%AppData%\ToolboxX\settings.json`)
- **Startzeit:** < 1 Sekunde
- **Ampel-System:** einheitlich über alle Module, als eigene Vektorgrafik
  (`Converters/AmpelIconFactory.cs`) statt Emoji – gleiche Optik auf jedem Windows,
  scharf auf jeder Auflösung, Gelb als Warndreieck (ohne Farbe unterscheidbar)
- **Schriften:** `src/PC-Toolbox/Fonts/` – Chakra Petch (Überschriften), Orbitron
  (Tagline), Rajdhani (Sidebar/Buttons). Alle **SIL Open Font License**
  (`Fonts/*/OFL.txt`), frei auch kommerziell, keine Namensnennungspflicht, als
  Resource in die EXE eingebettet – Nutzer müssen nichts installieren
- **Logo:** `src/PC-Toolbox/Assets/` – Alex' eigenes Toolbox-X-Logo, plus daraus
  zugeschnittenes quadratisches Zeichen und eine echte Multi-Size-`.ico` (16–256 px)
- **Zweisprachig:** Deutsch/Englisch über `Loc.T("deutsch", "english")`, Sprache
  in den Einstellungen umschaltbar, ohne Programmneustart
- **Mitgelieferte Fremd-Binaries** liegen bewusst in `vendor/` (nicht unter `src/`),
  jeweils unverändert mit Lizenz und Herkunftsvermerk: `memtest_vulkan` (zlib, VRAM-Test),
  `imdisk` (GPL-2.0, RAM-Disk), `pawnio` (GPL-2.0, echte Temperatursensoren). Die zwei
  echten **Treiber** (ImDisk, PawnIO) werden **nie still installiert**, sondern nur auf
  ausdrücklichen Klick mit normalem Installer-Dialog und UAC

## Modul-Vorbilder (Alex' Referenz-Tools, seit Jahren genutzt)

Als Vorbild empfohlen — aber bewusst nicht 1:1 nachgebaut, sondern erst
recherchiert wo Nutzer an diesen Tools scheitern, dann schlanker/besser gebaut:
- **Uninstall Tool 3.8.1** → Vorbild für Super-Uninstaller
- **Total Commander Ultimate Prime** → Vorbild für den Commander

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
Compile-Check) – aktuell **538 Tests** (Free-Konfiguration). Möglich, weil die
reine Logik (Deinstall-Befehl parsen, Namens-Abgleich für Leftover-Suche,
Pfad-Sicherheitsregeln, Autostart-/Dienste-Bewertung, Aufgaben-XML,
Gerätefehler-Codes, u.v.a.) in **`src/PC-ToolboxCore`** ausgelagert ist – ein
plattformneutrales `net8.0`-Projekt ohne WPF/Registry-Abhängigkeit.
`PC-Toolbox` (WPF, `net8.0-windows`) selbst kann auf Linux weiterhin nur
kompiliert, nicht ausgeführt werden – dafür braucht es echtes Windows oder
eine Windows Sandbox/VM.

**Auf einigen Modulen braucht Windows Administrator-Rechte**, sonst bleiben sie leer
statt zu lügen: Laufwerke (SMART über `root\WMI`) und Temperatur (ACPI-Thermalzone).

## Setup-Programm bauen

```powershell
dotnet tool install --global wix --version 5.0.2   # einmalig
.\build-setup.ps1
```

Ergebnis: `setup\bin\ToolboxX-Setup.msi`.

Das Skript macht drei Schritte: Release-Publish, Kontrolle des Publish-Ordners,
MSI-Build. Die Version wird aus der `.csproj` gelesen, steht also nur an einer Stelle.

**Warum self-contained (und damit so groß)?** Ohne self-contained müsste der Nutzer
erst die .NET-8-Desktop-Runtime installieren – und genau das ist für die Zielgruppe
("Leute, die niemanden zum Fragen haben") die Hürde, an der es scheitert. Ein größerer
Download ist der bessere Handel als ein Setup, das mit einer Runtime-Fehlermeldung
abbricht.

**Bewusste Entscheidungen im Installer** (Details als Kommentar in `setup/ToolboxX.wxs`):
kein Windows-Dienst, kein Autostart-Eintrag (Toolbox X ist ein Werkzeug, das man
startet – genau das kritisiert das Autostart-Modul an anderer Software), keine stille
Treiber-Installation, abwählbare Desktop-Verknüpfung, und die Snapshot-Sicherungen
unter `%ProgramData%\PC-Toolbox\Restore` bleiben beim Deinstallieren liegen (das sind
Nutzerdaten).

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
