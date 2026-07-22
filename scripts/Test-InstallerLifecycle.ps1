[CmdletBinding()]
param(
    [string]$IsccPath,
    [string]$PublishDirectory,
    [string]$SandboxRoot,
    [switch]$KeepSandbox
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-IsccPath {
    param([string]$RequestedPath)

    $candidates = @(
        $RequestedPath,
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
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

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "$FilePath failed with exit code $($process.ExitCode)."
    }
}

function Assert-Path {
    param([string]$LiteralPath, [string]$Description)
    if (-not (Test-Path -LiteralPath $LiteralPath)) {
        throw "$Description was not found: $LiteralPath"
    }
}

function Assert-Preserved {
    param([hashtable]$Markers)
    foreach ($entry in $Markers.GetEnumerator()) {
        Assert-Path -LiteralPath $entry.Value -Description "$($entry.Key) preservation marker"
    }
}

function Compile-SandboxInstaller {
    param(
        [string]$Compiler,
        [string]$InstallerScript,
        [string]$Version,
        [string]$BaseName,
        [string]$PublishPath,
        [string]$OutputPath,
        [string]$DataPath
    )

    $compilerOutput = & $Compiler @(
        '/Qp',
        "/O$OutputPath",
        "/F$BaseName",
        "/DAppVersion=`"$Version`"",
        "/DPublishDir=`"$PublishPath`"",
        '/DInstallerPrivileges="lowest"',
        "/DSharedDataRoot=`"$DataPath`"",
        "/DInstallerBaseName=`"$BaseName`"",
        $InstallerScript
    )
    $compilerExitCode = $LASTEXITCODE
    $compilerOutput | ForEach-Object { Write-Host $_ }
    if ($compilerExitCode -ne 0) {
        throw "ISCC failed for sandbox installer version $Version with exit code $compilerExitCode."
    }

    $setupPath = Join-Path $OutputPath "$BaseName.exe"
    Assert-Path -LiteralPath $setupPath -Description "Sandbox installer $Version"
    return $setupPath
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$compiler = Resolve-IsccPath -RequestedPath $IsccPath
$installerScript = Join-Path $repositoryRoot 'installer\EunSlip.iss'
if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64\1.0.0'
}
if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'EunSlip.exe') -PathType Leaf)) {
    throw "Publish output is missing. Run scripts\Build-Installer.ps1 first: $PublishDirectory"
}

if ([string]::IsNullOrWhiteSpace($SandboxRoot)) {
    $SandboxRoot = Join-Path ([IO.Path]::GetTempPath()) ("eunslip-installer-e2e-{0}" -f [Guid]::NewGuid().ToString('N'))
}
$SandboxRoot = [IO.Path]::GetFullPath($SandboxRoot)
$installRoot = Join-Path $SandboxRoot 'app'
$sharedDataRoot = Join-Path $SandboxRoot 'shared-data'
$setupOutput = Join-Path $SandboxRoot 'setup'
$logsRoot = Join-Path $SandboxRoot 'logs'
New-Item -ItemType Directory -Path $setupOutput, $logsRoot -Force | Out-Null

$completed = $false
try {
    $setup100 = Compile-SandboxInstaller -Compiler $compiler -InstallerScript $installerScript `
        -Version '1.0.0' -BaseName 'EunSlip-Setup-Test-1.0.0' -PublishPath $PublishDirectory `
        -OutputPath $setupOutput -DataPath $sharedDataRoot
    $setup101 = Compile-SandboxInstaller -Compiler $compiler -InstallerScript $installerScript `
        -Version '1.0.1' -BaseName 'EunSlip-Setup-Test-1.0.1' -PublishPath $PublishDirectory `
        -OutputPath $setupOutput -DataPath $sharedDataRoot

    Invoke-CheckedProcess -FilePath $setup100 -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
        "/DIR=`"$installRoot`"", "/LOG=`"$(Join-Path $logsRoot 'install-1.0.0.log')`""
    )
    Assert-Path -LiteralPath (Join-Path $installRoot 'EunSlip.exe') -Description 'Installed EunSlip executable'
    Assert-Path -LiteralPath (Join-Path $installRoot 'unins000.exe') -Description 'EunSlip uninstaller'

    $markers = @{
        database = Join-Path $sharedDataRoot 'database\history.preserved'
        oauth = Join-Path $sharedDataRoot 'oauth\token.preserved'
        stamp = Join-Path $sharedDataRoot 'stamp\stamp.preserved'
    }
    foreach ($entry in $markers.GetEnumerator()) {
        Set-Content -LiteralPath $entry.Value -Value "TASK-015-$($entry.Key)" -Encoding UTF8
    }

    Invoke-CheckedProcess -FilePath $setup101 -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
        "/DIR=`"$installRoot`"", "/LOG=`"$(Join-Path $logsRoot 'upgrade-1.0.1.log')`""
    )
    Assert-Path -LiteralPath (Join-Path $installRoot 'EunSlip.exe') -Description 'Upgraded EunSlip executable'
    Assert-Preserved -Markers $markers

    $uninstaller = Join-Path $installRoot 'unins000.exe'
    Invoke-CheckedProcess -FilePath $uninstaller -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
        "/LOG=`"$(Join-Path $logsRoot 'uninstall.log')`""
    )

    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ((Test-Path -LiteralPath (Join-Path $installRoot 'EunSlip.exe')) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
    }
    if (Test-Path -LiteralPath (Join-Path $installRoot 'EunSlip.exe')) {
        throw 'Application binaries remained after uninstall.'
    }
    Assert-Preserved -Markers $markers

    $completed = $true
    [pscustomobject]@{
        CleanInstall = 'PASS'
        Upgrade = 'PASS'
        UninstallRemovedBinaries = 'PASS'
        SharedDataPreserved = 'PASS'
        Sandbox = $SandboxRoot
    }
}
finally {
    if ($completed -and -not $KeepSandbox) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $SandboxRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $SandboxRoot) -notlike 'eunslip-installer-e2e-*') {
            throw "Refusing to remove non-sandbox path: $SandboxRoot"
        }
        Remove-Item -LiteralPath $SandboxRoot -Recurse -Force
    }
    elseif (-not $completed) {
        Write-Warning "Lifecycle verification failed; sandbox retained at $SandboxRoot"
    }
}
