# Moonlight Forwarder Generator

O MFG e um Companion para preparar jogos de PC para abertura direta no Nintendo Switch:

```text
Forwarder NSP -> Moonlight-Switch -> Sunshine -> jogo
```

O MVP nao usa Agent, named pipes ou launcher intermediario. O Companion centraliza o registro de jogos, a sincronizacao com Sunshine, os favoritos do Moonlight-Switch e a geracao de NSP.

## Estado atual

- `MFG.App`: Companion WPF, ponto unico de interacao.
- `MFG.Common`: modelos compartilhados da biblioteca.
- Biblioteca SQLite em `%LocalAppData%\MoonlightForwarderGenerator\mfg.db`.
- O cadastro armazena nome, executavel, aplicacao Sunshine, favorito Moonlight, NSP e status.
- Quando Sunshine esta instalado, o cadastro cria ou atualiza automaticamente a aplicacao correspondente no `apps.json`.
- O Companion detecta o cartao SD quando contem a instalacao esperada do Moonlight-Switch.
- Cada jogo recebe um Title ID unico. O gerador reconstrui um NSP novo com metadados, icone e argumentos `--ip`, `--appid` e `--appname` para o Moonlight.
- O NSP e mantido em `%LocalAppData%\MoonlightForwarderGenerator\forwarders`; com o cartao SD detectado, uma copia atomica tambem e enviada para `SD:\switch`.
- O backend usa `@tootallnate/hacbrewpack` e `@tootallnate/nacp`, derivados do projeto MIT [TooTallNate/switch-tools](https://github.com/TooTallNate/switch-tools). Consulte `tools/nsp-generator/THIRD-PARTY-NOTICES.md`.
- A chave fornecida pelo usuario fica protegida pelo Windows; para gerar, ela e decriptada apenas em uma pasta temporaria aleatoria, removida ao final.
- A sincronizacao de favoritos no `settings.json` do Moonlight-Switch ainda depende de confirmar o formato de um arquivo real desse aplicativo.
- **Configurar Switch** abre o assistente para importar a chave e explicar o pareamento inicial.
- **Preparar Sunshine** instala Sunshine somente apos confirmacao, cria a configuracao inicial sem sobrescrever aplicacoes existentes e inicia o servico.

## Executar

```powershell
dotnet run --project src/MFG.App
```
