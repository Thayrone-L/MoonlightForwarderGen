# Solução de problemas

**Português** | [English](Troubleshooting.en.md)

Comece fechando e abrindo o MFG novamente. Na tela principal, use **Configurações** para retomar o assistente e **Atualizar** para sincronizar novamente o cartão SD.

## O aplicativo não abre

- Extraia o ZIP por completo; não execute `MFG.App.exe` diretamente dentro dele.
- Confirme que o download veio da página oficial de Releases.
- No SmartScreen, use **Mais informações → Executar assim mesmo** somente depois dessa confirmação.
- Não remova `node.exe`, a pasta `nsp-generator` ou a pasta `switch` do pacote.

## Sunshine não fica pronto

1. Aguarde alguns segundos após a instalação.
2. Confirme o pedido de administrador do Windows.
3. Abra **Configurações → Preparar computador** novamente.
4. Verifique se firewall ou antivírus bloqueou o Sunshine.
5. Confirme que a interface web do Sunshine abre normalmente no computador.

## A chave não é aceita

- Selecione um arquivo `.keys` não vazio extraído do seu próprio console.
- Não renomeie arquivos aleatórios para `prod.keys`.
- Execute o MFG no mesmo usuário do Windows usado na importação; a proteção é vinculada a esse usuário.

## O cartão SD não é detectado

- Confirme que o Windows atribuiu uma letra à unidade.
- Verifique a existência de `SD:/switch/Moonlight-Switch/`.
- Para concluir o pareamento, deve existir `SD:/switch/Moonlight-Switch/settings.json`.
- Ejete e reconecte o cartão; depois clique em **Verificar cartão SD** ou **Atualizar**.

## O MFG Updater não aparece

Conecte o SD ao computador e clique em **Atualizar**. Depois confirme se existe:

```text
SD:/switch/MFG-Updater/MFG-Updater.nro
```

Se o arquivo existir, abra o Homebrew Menu novamente.

## O jogo não aparece no Updater

No cartão, confirme a presença de:

```text
SD:/switch/MFG/manifest.json
SD:/switch/MFG/MFG - Nome do jogo.nsp
```

Volte ao computador, selecione o jogo no MFG e clique em **Adicionar ao Switch**. Em seguida, clique em **Atualizar** com o SD conectado.

## Integridade inválida

Não tente instalar o arquivo indicado. Reconecte o cartão ao computador e sincronize novamente. O MFG recalcula e confere o SHA-256 depois da cópia.

## O atalho abre o Moonlight, mas não o jogo

- Teste primeiro se o Moonlight-Switch consegue abrir o computador normalmente.
- Confirme que Sunshine está em execução.
- Reabra o MFG e use a verificação da biblioteca.
- Se o jogo foi movido ou reinstalado, remova o registro antigo e adicione o executável correto.

## Logs para relatar um problema

No Windows, clique no botão **•••** no canto superior direito para abrir a pasta de logs. Ela também pode ser acessada em:

```text
%LocalAppData%\MoonlightForwarderGenerator\logs
```

No Switch:

```text
SD:/switch/MFG/logs/mfg-updater.log
```

Antes de publicar um log, revise seu conteúdo e remova caminhos ou nomes que considere pessoais. Nunca publique `prod.keys`.

Ao abrir uma issue, informe:

1. versão do MFG;
2. versão do Windows;
3. etapa em que ocorreu a falha;
4. mensagem exibida;
5. passos para reproduzir;
6. trecho relevante do log.
