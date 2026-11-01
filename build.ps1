param(
    [string]$GameDir = $env:SUPERMARKET_TOGETHER_DIR
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw 'Pass -GameDir with your Supermarket Together installation directory.'
}
$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
& dotnet build (Join-Path $PSScriptRoot 'BetterSupermarketTogether.csproj') --configuration Release "-p:GameDir=$resolvedGameDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
