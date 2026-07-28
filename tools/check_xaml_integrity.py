#!/usr/bin/env python3
"""ISY IEM-1001 - XAML-Vorab-Check.

Findet zwei Fehlerklassen, die `dotnet build` NICHT erkennt, weil WPF sie erst
beim tatsächlichen Fensteraufbau prüft (und der läuft nur unter Windows, nicht
auf dem Pi):

  1. {StaticResource X} ohne passenden x:Key="X" irgendwo im Projekt
     -> XamlParseException beim Start (z.B. CountToVisibilityConverter, 19.07.2026)
  2. Text-Bindungen (Run.Text/TextBlock.Text) auf einen Indexer-Pfad "[Key]"
     ohne explizites Mode=OneWay, wenn die Quelle ein bekannter nur-lesbarer
     Indexer ist (LocalizationService.Instance)
     -> XamlParseException beim Start (19.07.2026)

Zusätzlich (dritte Fehlerklasse vom selben Abend):
  3. .cs-Dateien unter src/shared/, die in keiner .csproj per <Compile Include>
     eingebunden sind -> CS0246 "Type not found" beim Bauen

Nutzung:
    python3 tools/check_xaml_integrity.py

Exit-Code 0 = sauber, 1 = mindestens ein Fund. Gedacht als Schritt VOR jedem
`dotnet publish`, das an Alex rausgeht.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"

KEY_RE = re.compile(r'x:Key="([^"]+)"')
STATIC_RES_RE = re.compile(r'\{StaticResource\s+([A-Za-z0-9_.]+)')
# Nur <Run>-Elemente: Run.Text bindet in WPF standardmaessig TwoWay (anders als
# z.B. TextBlock.Text, das OneWay-Default hat) - deshalb nur hier relevant.
RUN_BLOCK_RE = re.compile(r'<Run\b.*?(?:/>|</Run>)', re.DOTALL)
COMPILE_INCLUDE_RE = re.compile(r'<Compile Include="\.\.\\+shared\\+([A-Za-z0-9_]+)\.cs"')


def find_files(pattern):
    return list(SRC.rglob(pattern))


def check_static_resources():
    errors = []
    defined_keys = set()
    for f in find_files("*.xaml"):
        text = f.read_text(encoding="utf-8", errors="ignore")
        defined_keys.update(KEY_RE.findall(text))

    for f in find_files("*.xaml"):
        text = f.read_text(encoding="utf-8", errors="ignore")
        for lineno, line in enumerate(text.splitlines(), start=1):
            for m in STATIC_RES_RE.finditer(line):
                key = m.group(1)
                # x:Static / Type-Referenzen wie StaticResource ausschließen (kein Key-Lookup)
                if key not in defined_keys:
                    errors.append(
                        f"{f.relative_to(ROOT)}:{lineno}: "
                        f"StaticResource '{key}' hat keinen passenden x:Key im Projekt "
                        f"-> XamlParseException beim Start"
                    )
    return errors


def check_readonly_indexer_bindings():
    errors = []
    for f in find_files("*.xaml"):
        text = f.read_text(encoding="utf-8", errors="ignore")
        for m in RUN_BLOCK_RE.finditer(text):
            block = m.group(0)
            if "LocalizationService.Instance" not in block:
                continue
            if "Path=[" not in block and 'Path="[' not in block:
                continue
            if "Mode=" in block:
                continue
            lineno = text.count("\n", 0, m.start()) + 1
            errors.append(
                f"{f.relative_to(ROOT)}:{lineno}: "
                f"<Run>-Bindung auf LocalizationService-Indexer ohne explizites Mode=OneWay "
                f"-> Run.Text bindet standardmaessig TwoWay, Indexer ist nur lesbar -> Crash"
            )
    return errors


def check_resx_duplicate_keys():
    """Doppelte <data name="X"> in einer .resx sind gueltiges XML, aber beim
    Kompilieren zu .resources gewinnt (implementierungsabhaengig) meist der
    letzte Eintrag - der erste ist dann totes Gewicht, das bei Aenderungen
    faelschlich fuer die aktive Quelle gehalten wird (siehe LangRestartHint/
    Tray*-Duplikate, 20.07.2026)."""
    errors = []
    name_re = re.compile(r'<data name="([^"]+)"')
    for f in find_files("*.resx"):
        text = f.read_text(encoding="utf-8", errors="ignore")
        seen = {}
        for lineno, line in enumerate(text.splitlines(), start=1):
            m = name_re.search(line)
            if not m:
                continue
            name = m.group(1)
            if name in seen:
                errors.append(
                    f"{f.relative_to(ROOT)}:{lineno}: doppelter ResX-Key '{name}' "
                    f"(erstes Vorkommen Zeile {seen[name]}) -> einen der beiden entfernen"
                )
            else:
                seen[name] = lineno
    return errors


def check_shared_files_wired_up():
    shared_dir = SRC / "shared"
    if not shared_dir.exists():
        return []

    csproj_files = find_files("*.csproj")
    wired = set()
    for f in csproj_files:
        text = f.read_text(encoding="utf-8", errors="ignore")
        wired.update(COMPILE_INCLUDE_RE.findall(text))

    errors = []
    for cs_file in shared_dir.glob("*.cs"):
        name = cs_file.stem
        if name not in wired:
            errors.append(
                f"{cs_file.relative_to(ROOT)}: nicht in irgendeiner .csproj per "
                f"<Compile Include> eingebunden -> 'Type not found' beim Bauen, "
                f"falls andere Dateien Typen daraus verwenden"
            )
    return errors


def main():
    all_errors = []
    all_errors += check_static_resources()
    all_errors += check_readonly_indexer_bindings()
    all_errors += check_resx_duplicate_keys()
    all_errors += check_shared_files_wired_up()

    if not all_errors:
        print("OK - keine bekannten Fehlerklassen gefunden.")
        return 0

    print(f"{len(all_errors)} Fund(e):\n")
    for e in all_errors:
        print(f"  - {e}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
