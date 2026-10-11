<#
.SYNOPSIS
    Installe le plugin Exposure Budget dans N.I.N.A.

.DESCRIPTION
    Le plugin est copie dans le dossier scanne par N.I.N.A. :
        %LOCALAPPDATA%\NINA\Plugins\<version>\ExposureBudget\
    ou <version> est le dossier de plugins attendu par l'installation de N.I.N.A.
    detectee (metadonnee "PluginMinimumApplicationVersion" de NINA.Plugin.dll).

    N.I.N.A. charge les plugins a son demarrage : redemarrez-le apres l'installation.

.EXAMPLE
    .\tools\install.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$NinaPath = 'C:\Program Files\N.I.N.A. - Nighttime Imaging ''N'' Astronomy',

    # Installe le dll construit s'il n'a pas ete mis a jour (sinon erreur).
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pluginName = 'ExposureBudget'
$outputFolder = Join-Path $root "NINA.Plugin.ExposureBudget\bin\$Configuration\net8.0-windows"
$pluginDll = Join-Path $outputFolder 'NINA.Plugin.ExposureBudget.dll'

if (-not (Test-Path $pluginDll)) {
    throw "Plugin introuvable : $pluginDll. Lancez d'abord tools\build.ps1 -Configuration $Configuration."
}

<#
    N.I.N.A. range les plugins dans un dossier dont le nom correspond a la version
    minimale d'application declaree par NINA.Plugin.dll (3.0.0 pour la version stable
    actuelle). La valeur est lue directement dans l'assembly pour rester juste meme
    apres une mise a jour de N.I.N.A.
#>
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
$targetFolder = Join-Path $env:LOCALAPPDATA "NINA\Plugins\$pluginsVersionFolder\$pluginName"

$ninaProcess = Get-Process -Name 'NINA' -ErrorAction SilentlyContinue | Select-Object -First 1

Write-Host "Installation du plugin dans : $targetFolder" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null

$filesToCopy = @(
    'NINA.Plugin.ExposureBudget.dll',
    'NINA.Plugin.ExposureBudget.deps.json',
    'NINA.Plugin.ExposureBudget.pdb'
)

foreach ($file in $filesToCopy) {
    $source = Join-Path $outputFolder $file
    if (-not (Test-Path $source)) { continue }

    try {
        Copy-Item -Path $source -Destination $targetFolder -Force
        Write-Host "  copie : $file" -ForegroundColor DarkGray
    } catch [System.IO.IOException] {
        # N.I.N.A. garde ouvert le dll d'un plugin deja charge : il faut le fermer.
        $ninaPid = if ($ninaProcess) { " (PID $($ninaProcess.Id))" } else { '' }
        Write-Host ""
        Write-Warning "Impossible de remplacer $file : le fichier est verrouille par N.I.N.A.$ninaPid"
        Write-Host "  Fermez N.I.N.A. puis relancez l'installation :" -ForegroundColor Yellow
        Write-Host "      .\tools\install.ps1" -ForegroundColor Yellow
        Write-Host "  (le plugin actuellement installe reste utilisable en attendant)" -ForegroundColor DarkGray
        exit 2
    }
}

# Securite : le chargeur scanne tous les dll du dossier de plugin. Une copie d'une
# assembly N.I.N.A.* casserait le chargement : on nettoie si une ancienne version
# en avait laissee.
Get-ChildItem -Path $targetFolder -Filter 'NINA.*.dll' -File |
    Where-Object { $_.Name -ne 'NINA.Plugin.ExposureBudget.dll' } |
    ForEach-Object {
        Write-Warning "Assembly N.I.N.A. parasite supprimee : $($_.Name)"
        Remove-Item $_.FullName -Force
    }

# Le fichier de reglages du plugin est conserve d'une installation a l'autre.
$settingsFile = Join-Path $targetFolder 'ExposureBudget.settings'
if (Test-Path $settingsFile) {
    Write-Host "  reglages conserves : ExposureBudget.settings" -ForegroundColor DarkGray
}

# Migration depuis l'ancienne version du plugin, qui s'appelait "Number To Hours" :
# on recupere les reglages saisis par l'utilisateur, puis on retire l'ancien dossier de
# plugin, sinon deux panneaux identiques apparaitraient dans N.I.N.A.
$legacyFolderName = 'NumberToHours'
$legacyFolder = Join-Path (Split-Path -Parent $targetFolder) $legacyFolderName
if (Test-Path $legacyFolder) {
    $legacySettings = Join-Path $legacyFolder 'NumberToHours.settings'
    if ((Test-Path $legacySettings) -and -not (Test-Path $settingsFile)) {
        Copy-Item -Path $legacySettings -Destination $settingsFile -Force
        Write-Host "  reglages de l'ancienne version recuperes (NumberToHours.settings -> ExposureBudget.settings)" -ForegroundColor DarkGray
    }

    try {
        Remove-Item $legacyFolder -Recurse -Force
        Write-Host "  ancien plugin 'Number To Hours' retire" -ForegroundColor DarkGray
    } catch {
        Write-Host ""
        Write-Warning "L'ancien plugin est encore charge par N.I.N.A. : son dossier '$legacyFolder' n'a pas pu etre supprime."
        Write-Host "  Fermez N.I.N.A., puis relancez :" -ForegroundColor Yellow
        Write-Host "      .\tools\install.ps1" -ForegroundColor Yellow
        Write-Host "  (sinon, supprimez a la main le dossier ci-dessus : deux panneaux identiques apparaitraient)" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Plugin installe." -ForegroundColor Green
Write-Host "  1. Demarrez (ou redemarrez) N.I.N.A."
Write-Host "  2. Onglet Imagerie : le panneau 'Exposure Budget' apparait dans le volet des outils."
Write-Host "     Sinon : menu des panneaux de l'onglet Imagerie, ou Options > Plugins pour verifier le chargement."
