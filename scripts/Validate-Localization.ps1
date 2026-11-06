[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-XamlCatalog([string]$Path) {
    [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $catalog = @{}
    foreach ($node in $document.DocumentElement.ChildNodes) {
        $key = $node.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml')
        if (-not [string]::IsNullOrWhiteSpace($key)) { $catalog[$key] = $node.InnerText }
    }
    return $catalog
}

function Add-JsonLeaves([object]$Value, [string]$Prefix, [hashtable]$Catalog) {
    foreach ($property in $Value.PSObject.Properties) {
        $key = if ($Prefix) { "$Prefix/$($property.Name)" } else { $property.Name }
        if ($property.Value -is [string]) { $Catalog[$key] = $property.Value }
        elseif ($null -ne $property.Value) { Add-JsonLeaves $property.Value $key $Catalog }
    }
}

function Get-JsonCatalog([string]$Path) {
    $catalog = @{}
    $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-JsonLeaves $json '' $catalog
    return $catalog
}

function Get-Placeholders([string]$Text, [string]$Pattern) {
    return @([regex]::Matches($Text, $Pattern) | ForEach-Object Value | Sort-Object)
}

function Compare-Catalog([string]$Name, [hashtable]$Fallback, [hashtable]$Translation, [string]$PlaceholderPattern) {
    $errors = [System.Collections.Generic.List[string]]::new()
    foreach ($key in $Fallback.Keys) {
        if (-not $Translation.ContainsKey($key)) { $errors.Add("[$Name] chave ausente: $key"); continue }
        if ([string]::IsNullOrWhiteSpace($Translation[$key])) { $errors.Add("[$Name] texto vazio: $key") }
        $expected = Get-Placeholders $Fallback[$key] $PlaceholderPattern
        $actual = Get-Placeholders $Translation[$key] $PlaceholderPattern
        if (($expected -join '|') -ne ($actual -join '|')) {
            $errors.Add("[$Name] placeholders diferentes em '$key': esperado '$($expected -join ', ')', recebido '$($actual -join ', ')'")
        }
    }
    foreach ($key in $Translation.Keys) {
        if (-not $Fallback.ContainsKey($key)) { $errors.Add("[$Name] chave sem fallback em inglês: $key") }
    }
    return $errors
}

$wpfBase = Get-XamlCatalog (Join-Path $root 'src/MFG.App/Localization/Strings.en.xaml')
$wpfPt = Get-XamlCatalog (Join-Path $root 'src/MFG.App/Localization/Strings.pt-BR.xaml')
$nroBase = Get-JsonCatalog (Join-Path $root 'switch/MFG.Updater/lib/borealis/resources/i18n/en-US/mfg.json')
$nroPt = Get-JsonCatalog (Join-Path $root 'switch/MFG.Updater/lib/borealis/resources/i18n/pt-BR/mfg.json')

$errors = @()
$errors += Compare-Catalog 'Companion pt-BR' $wpfBase $wpfPt '\{\d+\}'
$errors += Compare-Catalog 'NRO pt-BR' $nroBase $nroPt '\{\}'
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Localização válida: $($wpfBase.Count) textos no Companion e $($nroBase.Count) textos no NRO."
