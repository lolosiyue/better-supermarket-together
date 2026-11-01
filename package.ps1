param(
    [string]$GameDir = $env:SUPERMARKET_TOGETHER_DIR
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BetterSupermarketTogether.csproj')
$version = $project.Project.PropertyGroup.Version
$artifactDir = Join-Path $PSScriptRoot 'artifacts'
$stageDir = Join-Path $artifactDir 'package'
$pluginDir = Join-Path $stageDir 'BepInEx\plugins\BetterSupermarketTogether'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\BetterSupermarketTogether.dll') -Destination $pluginDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\BetterSupermarketTogether_i18n_zhTW.tsv') -Destination $pluginDir
$companionSource = Join-Path $PSScriptRoot 'assets\companion'
if (Test-Path -LiteralPath $companionSource) {
    $model = Join-Path $companionSource 'DeepSeek-chan.vrm'
    if (!(Test-Path -LiteralPath $model)) { throw 'Run ./download-model.ps1 to fetch the licensed companion model before packaging.' }
    if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9fa455e48ee2271dc7c2b62f2f2edcd47709406ca89067b6f544979589958f42') { throw 'Companion model checksum mismatch.' }
    $companionTarget = Join-Path $pluginDir 'Companion'
    New-Item -ItemType Directory -Path $companionTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $companionSource -File | Copy-Item -Destination $companionTarget
}
foreach ($name in @('README.md', 'README.en.md', 'CHANGELOG.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stageDir
}
$docsDir = Join-Path $stageDir 'docs'
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'docs') -File | Copy-Item -Destination $docsDir
$archivePath = Join-Path $artifactDir "BetterSupermarketTogether-$version.zip"
$archiveItems = @(
    (Join-Path $stageDir 'BepInEx'),
    (Join-Path $stageDir 'README.md'),
    (Join-Path $stageDir 'README.en.md'),
    (Join-Path $stageDir 'CHANGELOG.md'),
    (Join-Path $stageDir 'LICENSE')
    (Join-Path $stageDir 'docs')
)
Compress-Archive -LiteralPath $archiveItems -DestinationPath $archivePath -Force
Write-Output "Package: $archivePath"
