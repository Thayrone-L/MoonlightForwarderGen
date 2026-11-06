param(
    [string] $Version = '0.1.0-alpha.1',
    [string] $Runtime = 'win-x64',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\release')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not $outputRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "A pasta de saída deve estar dentro de $artifactsRoot"
}
$publishDirectory = Join-Path $outputRoot "MoonlightForwarderGenerator-$Version-$Runtime"
$archivePath = "$publishDirectory.zip"

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Push-Location (Join-Path $repositoryRoot 'tools\nsp-generator')
try {
    npm ci --no-audit --no-fund
}
finally {
    Pop-Location
}

dotnet test (Join-Path $repositoryRoot 'MoonlightForwarderGenerator.sln') -c Release
dotnet publish (Join-Path $repositoryRoot 'src\MFG.App\MFG.App.csproj') `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDirectory

$nodeCommand = Get-Command node -ErrorAction Stop
Copy-Item -LiteralPath $nodeCommand.Source -Destination (Join-Path $publishDirectory 'node.exe')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $publishDirectory

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archivePath.sha256" -Value "$hash  $([IO.Path]::GetFileName($archivePath))"

Write-Output $archivePath
