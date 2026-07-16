# Moonlight Forwarder Generator — contexto inicial

## Workspace oficial

Trabalhe exclusivamente neste diretório:

```text
C:\Users\thayr\OneDrive\Documentos\Forwarder para moonlight
```

Use esse diretório como workspace gravável. Não implemente alterações na pasta temporária do Codex.

## Objetivo do produto

Criar um ecossistema para iniciar jogos de PC pelo menu HOME do Nintendo Switch, usando Forwarder NSP → Moonlight → Sunshine → Agent local. O Agent recebe um `GameId`, resolve o executável configurado e inicia o jogo automaticamente.

## Estado atual do MVP

- Solution .NET 8: `MoonlightForwarderGenerator.sln`.
- Projetos ativos:
  - `src/MFG.App`: interface WPF.
  - `src/MFG.Agent`: processo local em segundo plano.
  - `src/MFG.Common`: contratos e cliente de comunicação.
- Comunicação App → Agent: named pipe local `MoonlightForwarderGenerator.Agent`, JSON delimitado por quebra de linha.
- Banco: SQLite com EF Core em `%LocalAppData%\MoonlightForwarderGenerator\mfg.db`.
- Tabela `Games`: `Id`, `ExecutablePath`, `Arguments`, `CreatedAt`, `UpdatedAt`.
- A App permite cadastrar `ID + executável` e solicitar a execução pelo ID.
- O Agent implementa os comandos `register-game` e `launch-game`.
- Tabela `SunshineInstallations`: `Id`, `IsInstalled`, `ExecutablePath`, `LastCheckedAt`.
- Na inicialização, o Agent procura o Sunshine em diretórios conhecidos e no Registro do Windows, atualizando essa tabela.
- A validação anterior encontrou o Sunshine em `C:\Program Files\Sunshine\sunshine.exe`.
- Compilação mais recente: `dotnet build MoonlightForwarderGenerator.sln --no-restore`, sem avisos nem erros.
- A App consulta o status do Sunshine ao abrir e exibe um aviso quando ele está ausente.
- A instalação guiada exige confirmação explícita, consulta a release estável oficial no GitHub, seleciona o instalador Windows compatível, abre o instalador interativo e atualiza o status após o encerramento.
- Ao iniciar (e também após a instalação guiada), o Agent garante que o Sunshine esteja em execução e verifica as portas TCP padrão `47989` e `47990` antes de informar que está pronto para conexão local.
- O projeto inclui `MFG.Launcher`, executável de linha de comando para encaminhar `launch-game <GameId>` ao Agent; ele será usado como comando das aplicações criadas no Sunshine.
- Ao cadastrar um jogo, o Agent salva o registro SQLite e cria ou atualiza automaticamente a aplicação `MFG - <GameId>` no `apps.json` do Sunshine, apontando para o launcher.

## Instalação guiada do Sunshine

- A App usa `get-sunshine-status` para consultar o Agent ao iniciar.
- Quando ausente, o botão **Instalar Sunshine** pede confirmação explícita antes de enviar `install-sunshine`.
- O Agent consulta a API de releases de `LizardByte/Sunshine`, ignora drafts e pre-releases, e escolhe o instalador oficial `.exe` (com fallback `.msi`) para AMD64 ou ARM64.
- O download é temporário; o instalador é aberto de forma interativa, permitindo UAC. Ao encerrar, o Agent redetecta o Sunshine e registra o resultado no SQLite.

## Regras de implementação

- Não incorporar uma versão fixa nem URL fixa do Sunshine: usar a API de releases do GitHub e ignorar pre-releases.
- Aceitar somente assets oficiais de instalador Windows; preferir AMD64 em máquinas x64.
- Tratar erros de rede, download, cancelamento do usuário e instalador encerrado com código diferente de zero.
- Manter a instalação dependente de confirmação explícita na UI.
- Não implementar atualização automática nesta etapa; somente detecção e instalação quando ausente.
- Após alterações, compilar a solution e validar o fluxo sem instalar software real sem uma confirmação humana na UI.
