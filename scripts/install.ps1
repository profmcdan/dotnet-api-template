<#
.SYNOPSIS
    Installs the dotnet-api-template CLI on Windows.

.DESCRIPTION
    The tool carries the project template inside it, so this is the only thing you need to
    install. Run it from a clone, or straight from the web:

        .\scripts\install.ps1

        irm https://raw.githubusercontent.com/profmcdan/dotnet-api-template/main/scripts/install.ps1 | iex

.PARAMETER Ref
    Branch or tag to install from. Defaults to main.

.PARAMETER InstallRoot
    Where the source checkout and build artifacts are kept. Defaults to ~\.dotnet-api-template.
#>
[CmdletBinding()]
param(
    [string] $RepoUrl     = 'https://github.com/profmcdan/dotnet-api-template.git',
    [string] $Ref         = 'main',
    [string] $InstallRoot = (Join-Path $HOME '.dotnet-api-template')
)

$ErrorActionPreference = 'Stop'

$PackageId   = 'DotnetApiTemplate.Cli'
$ProjectPath = 'tools\DotnetApiTemplate.Cli\DotnetApiTemplate.Cli.csproj'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Warn { param([string] $Message) Write-Host " warn $Message" -ForegroundColor Yellow }
function Stop-WithError { param([string] $Message) Write-Host "error $Message" -ForegroundColor Red; exit 1 }

# --- prerequisites ----------------------------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Stop-WithError 'The .NET SDK is not installed or not on PATH. See https://dotnet.microsoft.com/download'
}

$sdkVersion = (dotnet --version).Trim()
if ([int]($sdkVersion.Split('.')[0]) -lt 10) {
    Stop-WithError ".NET SDK 10.0 or later is required, but 'dotnet --version' reports $sdkVersion."
}

# --- locate the source ------------------------------------------------------------------------
# Run from a checkout and we build that; piped from the web and we clone instead.
$sourceDir = $null

if ($PSCommandPath) {
    $candidate = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
    if (Test-Path (Join-Path $candidate $ProjectPath)) {
        $sourceDir = $candidate
        Write-Step "Building from this checkout: $sourceDir"
    }
}

if (-not $sourceDir) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Stop-WithError 'git is required to fetch the repository.'
    }

    $sourceDir = Join-Path $InstallRoot 'src'

    if (Test-Path (Join-Path $sourceDir '.git')) {
        Write-Step "Updating $sourceDir"
        git -C $sourceDir fetch --quiet origin $Ref
        git -C $sourceDir checkout --quiet $Ref
        git -C $sourceDir reset --hard --quiet "origin/$Ref"
    }
    else {
        Write-Step "Cloning $RepoUrl"
        New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
        git clone --quiet --depth 1 --branch $Ref $RepoUrl $sourceDir
    }
}

if (-not (Test-Path (Join-Path $sourceDir $ProjectPath))) {
    Stop-WithError "Could not find $ProjectPath under $sourceDir."
}

# --- build and install ------------------------------------------------------------------------
$artifacts = Join-Path $InstallRoot 'artifacts'
if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

Write-Step 'Packing the CLI'
dotnet pack (Join-Path $sourceDir $ProjectPath) `
    --configuration Release `
    --output $artifacts `
    --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { Stop-WithError 'dotnet pack failed.' }

# Uninstall first, then install. `dotnet tool update` is a no-op when the version number has
# not changed, which would silently keep an older build after a re-run.
Write-Step 'Installing the global tool'
dotnet tool uninstall --global $PackageId 2>$null | Out-Null
$global:LASTEXITCODE = 0
dotnet tool install --global --add-source $artifacts $PackageId | Out-Null
if ($LASTEXITCODE -ne 0) { Stop-WithError 'dotnet tool install failed.' }

$toolsDir = Join-Path $HOME '.dotnet\tools'
$env:PATH = "$env:PATH;$toolsDir"

Write-Step 'Registering the bundled project template'
dotnet-api-template update | Out-Null

Write-Host ''
Write-Host "Installed. $(dotnet-api-template version)" -ForegroundColor Green
Write-Host ''
Write-Host '  dotnet-api-template new --project-name Acme.Billing --allow-grpc'
Write-Host ''

$userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
if ($userPath -notlike "*$toolsDir*") {
    Write-Warn "$toolsDir is not on your PATH. Add it for this user with:"
    Write-Host ''
    Write-Host "    [Environment]::SetEnvironmentVariable('PATH', `"`$env:PATH;$toolsDir`", 'User')"
    Write-Host ''
    Write-Host '  then open a new terminal.'
    Write-Host ''
}
