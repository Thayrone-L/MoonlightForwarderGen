# MFG Updater para Nintendo Switch

**Português** | [English](SwitchUpdater.en.md)

## Papel no produto

O MFG Updater é o complemento que adiciona ao menu HOME os forwarders preparados no computador. O Companion o copia automaticamente para `SD:/switch/MFG-Updater/MFG-Updater.nro` quando detecta o cartão SD.

A versão atual do componente é `0.5.4`. Ela é versionada independentemente do pacote Windows, atualmente `0.1.0-alpha.1`.

## Fluxo do usuário

1. Sincronizar a biblioteca pelo MFG no computador.
2. Colocar o cartão SD no Switch.
3. Abrir o MFG Updater pelo Homebrew Menu.
4. Selecionar um jogo cuja integridade esteja válida.
5. Confirmar **Adicionar ao menu HOME**.

## Limites de segurança

O Updater:

- lê somente `SD:/switch/MFG/manifest.json`;
- aceita contratos de manifesto v1 e v2;
- rejeita travessia de diretório;
- verifica o SHA-256 completo do NSP;
- aceita apenas NSP PFS0 contendo NCAs, sem tickets ou certificados;
- confere o tipo de conteúdo, CNMT e Title ID;
- instala somente após confirmação do usuário;
- ainda não remove títulos instalados.

Ele é deliberadamente limitado aos forwarders plaintext produzidos pelo MFG e não funciona como instalador de NSP de uso geral.

## Logs no Switch

Os diagnósticos ficam em `SD:/switch/MFG/logs/mfg-updater.log`. Ao atingir 512 KiB, o arquivo anterior é preservado como `mfg-updater.previous.log`. Falhas ao criar logs não bloqueiam o aplicativo.

## Compilação

São necessários devkitPro, devkitA64, libnx, `switch-libjson-c`, `switch-glfw`, `switch-mesa` e `switch-glm`. Borealis está incluída em `switch/MFG.Updater/lib/borealis`.

Com o ambiente devkitPro carregado:

```bash
cd switch/MFG.Updater
make
```
