# Protocolo

O MVP usa um named pipe local chamado `MoonlightForwarderGenerator.Agent` e mensagens JSON delimitadas por quebra de linha.

Exemplos de comandos:

```json
{ "name": "register-game", "gameId": "notepad", "executablePath": "C:\\Windows\\System32\\notepad.exe" }
{ "name": "launch-game", "gameId": "notepad" }
{ "name": "get-sunshine-status" }
{ "name": "install-sunshine" }
```

## MFG.Launcher

O `MFG.Launcher` é o executável chamado pelo Sunshine para acionar um jogo. Ele encaminha o ID ao Agent pelo named pipe e devolve um código de saída apropriado ao Sunshine:

```text
MFG.Launcher.exe launch-game <GameId>
```

O `MFG.Agent` precisa estar em execução antes do launcher ser chamado.

## Cadastro no Sunshine

Ao receber `register-game`, o Agent também cria ou atualiza uma entrada `MFG - <GameId>` no `apps.json` configurado pelo Sunshine. O comando da entrada chama o `MFG.Launcher` copiado para a pasta do Agent.
