[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version = '1.0.0',
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [string]$IsccPath,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-IsccPath {
    param([string]$RequestedPath)

    $candidates = @(
        $RequestedPath,
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'ISCC.exe was not found. Install Inno Setup 6 or pass -IsccPath.'
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\EunSlip.Desktop\EunSlip.Desktop.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\EunSlip.iss'
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$Runtime\$Version"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'installer\output'
}

$compiler = Resolve-IsccPath -RequestedPath $IsccPath
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Invoke-Checked -FilePath 'dotnet' -ArgumentList @(
    'publish', $projectPath,
    '--configuration', $Configuration,
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $publishDirectory,
    "-p:Version=$Version",
    '-p:PublishSingleFile=false'
)

$publishedExecutable = Join-Path $publishDirectory 'EunSlip.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Self-contained publish did not produce $publishedExecutable."
}

$installerBaseName = 'EunSlip-Setup-x64'
Invoke-Checked -FilePath $compiler -ArgumentList @(
    '/Qp',
    "/O$OutputDirectory",
    "/F$installerBaseName",
    "/DAppVersion=`"$Version`"",
    "/DPublishDir=`"$publishDirectory`"",
    "/DInstallerBaseName=`"$installerBaseName`"",
    $installerScript
)

$installerPath = Join-Path $OutputDirectory 'EunSlip-Setup-x64.exe'
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer compiler did not produce $installerPath."
}

$hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
[pscustomobject]@{
    Version = $Version
    Runtime = $Runtime
    SelfContained = $true
    PublishDirectory = $publishDirectory
    Installer = $installerPath
    Sha256 = $hash.Hash
}
