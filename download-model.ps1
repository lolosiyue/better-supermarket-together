param()
$ErrorActionPreference = 'Stop'
$assetDir = Join-Path $PSScriptRoot 'assets\companion'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$target = Join-Path $assetDir 'DeepSeek-chan.vrm'
$temp = $target + '.download'
$expected = '9fa455e48ee2271dc7c2b62f2f2edcd47709406ca89067b6f544979589958f42'
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/tegnike/deepseek-chan-vrm/3b28db3bdde8d2e73b698a060044e0d8ebdf8b7b/DeepSeek-chan.vrm' -OutFile $temp
if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Model checksum mismatch; existing model was not changed.' }
Move-Item -LiteralPath $temp -Destination $target -Force
Write-Output 'Pinned model downloaded and verified. See assets/companion/NOTICE.txt and LICENSE for credits and terms.'
