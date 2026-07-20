param([string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts\app"

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $root ".packages"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:APPDATA = Join-Path $root ".appdata"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet -or -not (& $dotnet --list-sdks)) {
    throw ".NET 8 SDK bulunamadi. https://dotnet.microsoft.com/download/dotnet/8.0 adresinden SDK'yi kurun."
}

$project = Join-Path $root "src\MailMigration.UI\MailMigration.UI.csproj"
& $dotnet restore $project -r $Runtime --configfile (Join-Path $root "NuGet.Config") -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "NuGet restore basarisiz oldu (exit code: $LASTEXITCODE)." }

& $dotnet publish $project -c Release -r $Runtime --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw "EXE publish basarisiz oldu (exit code: $LASTEXITCODE)." }