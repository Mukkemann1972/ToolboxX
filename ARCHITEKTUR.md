# PC-Toolbox – Architektur (V0.01)

## Projekt-Struktur

```
PC-Toolbox/
├── PC-Toolbox.sln
├── src/
│   ├── PC-Toolbox/                # Haupt-WPF-App (.NET 8)
│   │   ├── App.xaml(.cs)
│   │   ├── MainWindow.xaml(.cs)   # PDF24-ähnliches Layout
│   │   ├── Modules/               # Ein Modul = eine Kategorie
│   │   │   ├── Dashboard.xaml(.cs)
│   │   │   ├── DiskCheck.xaml(.cs)
│   │   │   ├── Bluescreen.xaml(.cs)
│   │   │   └── Temperature.xaml(.cs)
│   │   ├── Services/              # Daten-Beschaffer
│   │   │   ├── SystemInfoService.cs
│   │   │   ├── SmartService.cs
│   │   │   ├── DumpReader.cs
│   │   │   └── TemperatureService.cs
│   │   ├── Safety/                # 3-Phasen-Sicherheit
│   │   │   ├── SnapshotManager.cs
│   │   │   ├── RollbackEngine.cs
│   │   │   └── SafetyEnforcer.cs
│   │   ├── Models/                # Daten-Modelle
│   │   │   ├── DiskInfo.cs
│   │   │   ├── BlueshotEntry.cs
│   │   │   └── TempReading.cs
│   │   └── Hardware/              # 🔧 Hardware-Schnittstelle (später)
│   │       ├── SerialPort.cs      # Für Arduino/ESP-basierte Adapter
│   │       └── HardwareProbe.cs   # Autodetect angeschlossene Tools
│   └── PC-Toolbox.Tests/
├── hardware/                      # 🔧 Hardware-Pläne & Firmware
│   ├── docs/                      # Bauanleitungen, Stücklisten
│   └── firmware/                  # ESP/Arduino-Code
└── README.md
```

## Ampel-System (einheitlich)

| Farbe | Bedeutung | UI |
|-------|-----------|-----|
| ✅ Grün | Alles okay | Keine Aktion |
| ⚠️ Gelb | Prüfenswert | Lösungsvorschlag anzeigen |
| ❌ Rot | Kritisch | Sofort handeln! |

## Services (Datenquellen)

Alle Services sind asynchron und geben IMMER ein Ergebnis zurück (nie null):
- Bei Erfolg: Daten + Status
- Bei Fehler: Fehlermeldung + Lösungsvorschlag (z.B. "WMI nicht verfügbar → Als Admin starten")

### SystemInfoService.cs
- CPU (Name, Kerne, Auslastung %)
- RAM (gesamt, frei, belegt in GB)
- Windows-Version (Build, Edition)
- Motherboard (Hersteller, Modell)

### SmartService.cs
- Alle physischen Laufwerke (SSD/HDD/NVMe/USB)
- SMART-Werte: ReallocatedSectors, CurrentPendingSector, Temperature, PowerOnHours
- Ampel pro Laufwerk + Erklärung der Werte

### DumpReader.cs
- Liest `C:\Windows\Minidump\*.dmp` (letzte 5)
- Zeigt Datum, Fehlercode, Auslöser-Datei/Treiber
- Lösungsvorschläge für häufige Fehler

### TemperatureService.cs
- CPU-Temperatur (WMI/OpenHardwareMonitor)
- GPU-Temperatur (WMI)
- Laufwerks-Temperatur (via SMART)
- Ampel mit kritischen Grenzen

## 3-Phasen-Sicherheit

Noch nicht in V0.01 (reine Diagnose, keine schreibenden Aktionen).
Kommt mit V0.02 (Super-Uninstaller).

Vorbereitet im Ordner `Safety/`:
- `SnapshotManager.cs` – Registry + Dateien sichern
- `RollbackEngine.cs` – Snapshot wiederherstellen
- `SafetyEnforcer.cs` – Erzwingt Phase 1→2→3 für jedes Modul

## 🔧 Hardware-Integration (Zukunft)

Die Toolbox wird so gebaut, dass sie später **physische Adapter** ansteuern kann:
- **USB/Serielle Schnittstelle** → für Diagnose-Adapter
- **GPIO über USB** → für eigenständige Test-Hardware
- **Platine mit ESP32/Arduino** → für Low-Level-Tests (z.B. Maus-Signal-Analyse)

Der Ordner `hardware/` wächst parallel zur Software – Dokumentation und Firmware.
Siehe [HARDWARE_VISION.md](HARDWARE_VISION.md) für Details.

## Build & Deployment

```bash
# Debug (lokal testen)
dotnet build -c Debug

# Release (für Testverteilung)
dotnet publish -c Release -r win-x64 --self-contained false
# → Ausgabe in src/PC-Toolbox/bin/Release/net8.0-windows/win-x64/publish/
```

**Wichtig:** .NET 8 WPF läuft NUR auf Windows. Cloud Code kann kompilieren und testen.
