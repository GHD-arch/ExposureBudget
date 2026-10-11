<#
.SYNOPSIS
    Construit le plugin Exposure Budget, verifie la logique de calcul et le chargement
    du plugin, puis l'installe eventuellement dans N.I.N.A.

.EXAMPLE
    .\tools\build.ps1
    Construit le plugin en Release et lance les deux harnais de verification.

.EXAMPLE
    .\tools\build.ps1 -Install
    Construit, verifie puis installe le plugin dans %LOCALAPPDATA%\NINA\Plugins.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # Installe le plugin dans N.I.N.A. apres une construction reussie.
    [switch]$Install,

    # Ignore les harnais de verification.
    [switch]$NoVerify,

    # Dossier de l'installation de N.I.N.A. utilisee comme reference de compilation.
    [string]$NinaPath = 'C:\Program Files\N.I.N.A. - Nighttime Imaging ''N'' Astronomy'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# -p:UseSharedCompilation=false evite le serveur de compilation Roslyn, indisponible
# dans les environnements d'execution restreints (bac a sable, agent de build).
$commonArgs = @('-m:1', '-p:UseSharedCompilation=false', '--nologo', "-p:NinaSdkPath=$NinaPath")

function Invoke-Step {
    param(
        [string]$Title,
        [string]$Command,
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host "== $Title ==" -ForegroundColor Cyan

    # Les outils natifs ecrivent volontiers sur stderr : on desactive temporairement
    # l'arrete-sur-erreur et on controle explicitement le code de sortie.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command @Arguments
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }

    if ($code -ne 0) {
        throw "'$Command $($Arguments -join ' ')' a echoue (code $code)."
    }
}

function Invoke-Harness {
    param(
        [string]$Title,
        [string]$Dll
    )

    Write-Host ""
    Write-Host "== $Title ==" -ForegroundColor Cyan

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet $Dll
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }

    if ($code -ne 0) {
        throw "Le harnais '$Title' a signale des anomalies (code $code)."
    }
}

if (-not (Test-Path (Join-Path $NinaPath 'NINA.Plugin.dll'))) {
    Write-Warning "N.I.N.A. n'a pas ete trouve dans '$NinaPath' : utilisez -NinaPath pour indiquer son dossier."
    exit 1
}

$pluginProject = Join-Path $root 'NINA.Plugin.ExposureBudget\NINA.Plugin.ExposureBudget.csproj'
$verifyProject = Join-Path $root 'tools\verify\ExposureBudget.Verify.csproj'
$smokeTestProject = Join-Path $root 'tools\smoketest\ExposureBudget.SmokeTest.csproj'

$pluginOutput = Join-Path $root "NINA.Plugin.ExposureBudget\bin\$Configuration\net8.0-windows"
$pluginDll = Join-Path $pluginOutput 'NINA.Plugin.ExposureBudget.dll'
$verifyDll = Join-Path $root "tools\verify\bin\$Configuration\net8.0\ExposureBudget.Verify.dll"
$smokeTestDll = Join-Path $root "tools\smoketest\bin\$Configuration\net8.0-windows\ExposureBudget.SmokeTest.dll"

Invoke-Step "1/3 Construction du plugin ($Configuration)" 'dotnet' (@('build', $pluginProject, '-c', $Configuration) + $commonArgs)
Write-Host "   -> $pluginDll" -ForegroundColor DarkGray

if ($NoVerify) {
    Write-Host ""
    Write-Host "== 2-3/3 Verifications ignorees (-NoVerify) ==" -ForegroundColor Yellow
} else {
    Invoke-Step "2/3 Verification de la logique de calcul" 'dotnet' (@('build', $verifyProject, '-c', $Configuration) + $commonArgs)
    Invoke-Harness "Verification de la logique de calcul" $verifyDll

    Invoke-Step "3/3 Verification du chargement du plugin (facon N.I.N.A.)" 'dotnet' (@('build', $smokeTestProject, '-c', $Configuration) + $commonArgs)
    Invoke-Harness "Verification du chargement du plugin" $smokeTestDll
}

if ($Install) {
    & (Join-Path $PSScriptRoot 'install.ps1') -Configuration $Configuration -NinaPath $NinaPath
    $installCode = $LASTEXITCODE
    if ($installCode -eq 2) {
        # N.I.N.A. est ouvert et verrouille le dll deja installe : ce n'est pas un echec
        # de construction, l'installation reste a refaire apres avoir ferme N.I.N.A.
        Write-Host ""
        Write-Warning "Installation en attente : fermez N.I.N.A. puis lancez .\tools\install.ps1"
    } elseif ($installCode -ne 0) {
        throw "L'installation a echoue (code $installCode)."
    }
}

Write-Host ""
Write-Host "Termine. Plugin construit : $pluginDll" -ForegroundColor Green
