# Safety/ – Platzhalter für V0.02

Absichtlich leer in V0.01 (reine Diagnose, keine schreibenden Aktionen).
Kommt mit dem Super-Uninstaller in V0.02:

- `SnapshotManager.cs` – Registry + Dateien sichern
- `RollbackEngine.cs` – Snapshot wiederherstellen
- `SafetyEnforcer.cs` – erzwingt Phase 1→2→3 (Sichern → Ausführen → Verifizieren) für jedes Modul
