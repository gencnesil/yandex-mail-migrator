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
    $localSdk = "C:\tmp\dotnet8\dotnet.exe"
    if (Test-Path -LiteralPath $localSdk) { $dotnet = $localSdk }
    else { throw ".NET 8 SDK bulunamadı. https://dotnet.microsoft.com/download/dotnet/8.0 adresinden SDK'yı kurun." }
}
$project = Join-Path $root "src\MailMigration.UI\MailMigration.UI.csproj"
& $dotnet restore $project -r $Runtime --configfile (Join-Path $root "NuGet.Config") -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "NuGet geri yükleme başarısız oldu (çıkış kodu: $LASTEXITCODE)." }

& $dotnet publish $project -c Release -r $Runtime --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw "EXE yayınlama başarısız oldu (çıkış kodu: $LASTEXITCODE)." }

# Eski AssemblyName ile üretilmiş EXE başka klasörde kalıp yanlışlıkla
# çalıştırılmasın. Eski yolu kullananları da güncel uygulamaya yönlendir.
$legacyOutput = Join-Path $root "artifacts\publish"
if (Test-Path -LiteralPath $legacyOutput) {
    Copy-Item -LiteralPath (Join-Path $output "MailMigrationDesktop.exe") -Destination (Join-Path $legacyOutput "MailMigration.UI.exe") -Force
}
