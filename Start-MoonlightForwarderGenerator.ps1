param(
    [string] $ProjectRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$solutionPath = Join-Path $ProjectRoot 'MoonlightForwarderGenerator.sln'
$briefPath = Join-Path $PSScriptRoot 'MOONLIGHT_FORWARDER_GENERATOR_BRIEF.md'

if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "A solution não foi encontrada em: $solutionPath"
}

if (-not (Test-Path -LiteralPath $briefPath)) {
    throw "O briefing do projeto não foi encontrado em: $briefPath"
}

$brief = Get-Content -LiteralPath $briefPath -Raw
Set-Clipboard -Value $brief
Set-Location -LiteralPath $ProjectRoot

Write-Host "Projeto aberto em: $ProjectRoot" -ForegroundColor Green
Write-Host 'O resumo inicial foi copiado para a área de transferência.' -ForegroundColor Green
Write-Host 'Ao abrir a nova conversa, cole o resumo e solicite a próxima tarefa.' -ForegroundColor Yellow

if (Get-Command codex -ErrorAction SilentlyContinue) {
    & codex
}
else {
    Write-Host 'O comando Codex não foi encontrado. Abra o Codex e selecione esta pasta como workspace.' -ForegroundColor Yellow
}
