<#
    Baut das Setup-Programm fuer Toolbox X.

        .\build-setup.ps1

    Ergebnis: setup\bin\ToolboxX-Setup.msi

    Voraussetzung einmalig:
        dotnet tool install --global wix --version 5.0.2

    Warum self-contained (und damit ~170 MB)?
    Ohne self-contained muesste der Nutzer erst die .NET-8-Desktop-Runtime
    installieren. Fuer die Zielgruppe von Toolbox X - Leute, die niemanden zum
    Fragen haben - ist genau das die Huerde, an der es scheitert. Ein groesserer
    Download ist der bessere Handel als ein Setup, das mit einer Fehlermeldung
    ueber eine fehlende Runtime abbricht.

    Warum Single-File?
    Damit neben der EXE nur das liegt, was wirklich daneben liegen MUSS: die
    zwei Theme-Dateien (werden zur Laufzeit als Text gelesen) und die drei
    mitgelieferten Fremd-Programme (werden als eigene Prozesse gestartet).
    Statt ~200 DLLs sind so nur 11 Dateien im Installer zu pflegen.
#>

$ErrorActionPreference = 'Stop'

$Wurzel     = $PSScriptRoot
$PublishDir = Join-Path $Wurzel 'build\publish'
$Projekt    = Join-Path $Wurzel 'src\PC-Toolbox\PC-Toolbox.csproj'
$SetupProj  = Join-Path $Wurzel 'setup\ToolboxXSetup.wixproj'

# --- Version aus der .csproj lesen, damit sie nur an EINER Stelle steht ---
$csproj  = [xml](Get-Content $Projekt)
$Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
if (-not $Version) { throw "Keine <Version> in $Projekt gefunden." }
Write-Host "Version aus der .csproj: $Version" -ForegroundColor Cyan

# --- 1) Release-Publish ---
Write-Host "`n[1/3] Release-Publish (self-contained, Single-File)..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

dotnet publish $Projekt `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen." }

# --- 2) Pruefen, dass wirklich alles da ist ---
# Diese Kontrolle ist kein Selbstzweck: beim ersten Versuch fehlten die drei
# mitgelieferten .exe-Dateien im Publish, weil der Single-File-Bundler sie
# einpackt statt daneben zu legen (behoben per ExcludeFromSingleFile in der
# .csproj). Der Fehler faellt sonst NICHT auf - das Programm startet normal,
# nur GPU-Test, RAM-Disk und PawnIO funktionieren still nicht mehr.
Write-Host "`n[2/3] Publish-Ordner pruefen..." -ForegroundColor Cyan
$Pflicht = @(
    'PC-Toolbox.exe',
    'Themes\ToolboxXTheme.xaml',
    'Themes\ToolboxXThemeLight.xaml',
    'gpu-tools\memtest_vulkan\memtest_vulkan.exe',
    'gpu-tools\memtest_vulkan\LICENSE.txt',
    'ramdisk-tools\imdisk\imdiskinst.exe',
    'ramdisk-tools\imdisk\LICENSE.md',
    'ramdisk-tools\imdisk\HERKUNFT.txt',
    'hardware-tools\pawnio\PawnIO_setup.exe',
    'hardware-tools\pawnio\LICENSE.md',
    'hardware-tools\pawnio\HERKUNFT.txt'
)
$Fehlt = $Pflicht | Where-Object { -not (Test-Path (Join-Path $PublishDir $_)) }
if ($Fehlt) {
    throw "Im Publish-Ordner fehlen Dateien, die der Installer braucht:`n  " + ($Fehlt -join "`n  ")
}
Write-Host "  alle $($Pflicht.Count) Pflichtdateien vorhanden" -ForegroundColor Green

# --- 3) MSI bauen ---
Write-Host "`n[3/3] MSI bauen..." -ForegroundColor Cyan
dotnet build $SetupProj -c Release `
    "-p:PublishDir=$PublishDir" `
    "-p:ProduktVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "MSI-Build ist fehlgeschlagen." }

$Msi = Join-Path $Wurzel 'setup\bin\ToolboxX-Setup.msi'
if (-not (Test-Path $Msi)) { throw "MSI wurde nicht erzeugt: $Msi" }

$GroesseMb = [math]::Round((Get-Item $Msi).Length / 1MB, 1)
Write-Host "`nFertig: $Msi ($GroesseMb MB)" -ForegroundColor Green
Write-Host "Zum Testen bitte in einer Windows Sandbox oder VM installieren, nicht direkt auf dem Hauptsystem." -ForegroundColor Yellow
