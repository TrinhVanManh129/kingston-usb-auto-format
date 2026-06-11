[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$expectedDist = [System.IO.Path]::GetFullPath((Join-Path $root 'dist'))
$resolvedRoot = [System.IO.Path]::GetFullPath($root)

if (-not $expectedDist.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar)) {
    throw "Unsafe dist path: $expectedDist"
}

if (Test-Path -LiteralPath $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

Write-Host 'Running tests...'
dotnet run --project (Join-Path $root 'tests\CompanyUsbFormatter.Tests\CompanyUsbFormatter.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

$output = Join-Path $dist 'win-x64'
Write-Host 'Publishing self-contained single-file EXE...'
dotnet publish (Join-Path $root 'src\CompanyUsbFormatter\CompanyUsbFormatter.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $output 'KingstonUsbFormatter.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published EXE was not found: $exe"
}

$hash = Get-FileHash -LiteralPath $exe -Algorithm SHA256
$size = (Get-Item -LiteralPath $exe).Length

Write-Host ''
Write-Host "Build complete: $exe"
Write-Host "Size: $size bytes"
Write-Host "SHA256: $($hash.Hash)"
