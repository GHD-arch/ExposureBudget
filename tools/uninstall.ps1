<#
.SYNOPSIS
    Desinstalle le plugin Exposure Budget de N.I.N.A.

.EXAMPLE
    .\tools\uninstall.ps1
#>
[CmdletBinding()]
param(
    [string]$NinaPath = 'C:\Program Files\N.I.N.A. - Nighttime Imaging ''N'' Astronomy'
)

$ErrorActionPreference = 'Stop'
$pluginName = 'ExposureBudget'

function Get-NinaPluginFolderVersion {
    param([string]$NinaInstallPath)

    $fallback = '3.0.0'
    $pluginFramework = Join-Path $NinaInstallPath 'NINA.Plugin.dll'
    if (-not (Test-Path $pluginFramework)) { return $fallback }

    $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($pluginFramework))
    $key = 'PluginMinimumApplicationVersion'
    $index = $text.IndexOf($key, [System.StringComparison]::Ordinal)

    while ($index -ge 0) {
        $slice = $text.Substring($index, [Math]::Min(64, $text.Length - $index))
        $match = [regex]::Match($slice, "$key[\x00-\x20]*([0-9]+\.[0-9]+\.[0-9]+)")
        if ($match.Success) { return $match.Groups[1].Value }
        $index = $text.IndexOf($key, $index + 1, [System.StringComparison]::Ordinal)
    }

    return $fallback
}

$pluginsVersionFolder = Get-NinaPluginFolderVersion -NinaInstallPath $NinaPath
$baseFolder = Join-Path $env:LOCALAPPDATA "NINA\Plugins\$pluginsVersionFolder"
$targetFolder = Join-Path $baseFolder $pluginName

if (Get-Process -Name 'NINA' -ErrorAction SilentlyContinue) {
    Write-Warning "N.I.N.A. est en cours d'execution : fermez-le pour liberer le dll du plugin."
}

if (-not (Test-Path $targetFolder)) {
    Write-Host "Le plugin n'est pas installe ($targetFolder)." -ForegroundColor Yellow
    exit 0
}

# Le chargeur de plugins scanne aussi les sous-dossiers du dossier de version :
# on supprime le dossier du plugin, et une eventuelle copie directe du dll.
$settingsFile = Join-Path $targetFolder 'ExposureBudget.settings'
if (Test-Path $settingsFile) {
    Write-Host "Le fichier de reglages $settingsFile est supprime avec le dossier du plugin." -ForegroundColor DarkGray
}

Get-ChildItem -Path $targetFolder -File -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Force }
Remove-Item $targetFolder -Recurse -Force

$strayDll = Join-Path $baseFolder 'NINA.Plugin.ExposureBudget.dll'
if (Test-Path $strayDll) { Remove-Item $strayDll -Force }

# Reste eventuel de l'ancien nom du plugin ("Number To Hours").
$legacyFolder = Join-Path $baseFolder 'NumberToHours'
if (Test-Path $legacyFolder) {
    $legacyStrayDll = Join-Path $baseFolder 'NINA.Plugin.NumberToHours.dll'
    if (Test-Path $legacyStrayDll) { Remove-Item $legacyStrayDll -Force }
    Remove-Item $legacyFolder -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Ancien dossier 'Number To Hours' supprime : $legacyFolder" -ForegroundColor DarkGray
}

Write-Host "Plugin desinstalle de : $targetFolder" -ForegroundColor Green
Write-Host "Redemarrez N.I.N.A. pour que le panneau disparaisse."
