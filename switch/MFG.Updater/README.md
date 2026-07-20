# MFG Updater para Nintendo Switch

Homebrew responsável por aplicar no console a biblioteca de forwarders preparada pelo
Moonlight Forwarder Generator.

## Marco atual

A versão `0.5.0` substitui a interface de console por uma interface gráfica
acelerada baseada em Borealis, coerente com o aplicativo do computador. Ela:

- abre `SD:/switch/MFG/manifest.json`;
- aceita apenas o contrato `schemaVersion: 1`;
- rejeita nomes de arquivo com navegação de diretório;
- verifica o SHA-256 completo de cada NSP diretamente no cartão SD;
- lista os forwarders, arquivos ausentes e remoções solicitadas;
- permite selecionar e instalar um forwarder válido após confirmação;
- aceita somente NSP PFS0 com NCAs, sem tickets, certificados ou arquivos extras;
- confere se o CNMT é uma aplicação e se seu Title ID coincide com o manifesto;
- registra o conteúdo no NCM e publica a aplicação no menu HOME;
- ainda não remove títulos.

Quando o NSP e o hash do manifesto divergem, o NRO bloqueia a instalação e
orienta reconectar o cartão ao Companion. A sincronização recopia o arquivo,
força a gravação física, confirma novamente o SHA-256 no SD e somente então
publica o manifesto.

## Logs no Switch

O complemento registra diagnóstico persistente em
`SD:/switch/MFG/logs/mfg-updater.log`. O arquivo contém carregamento do manifesto,
resultado da integridade, Title ID selecionado e cada chamada NCM/NS com seu código
hexadecimal. Ao atingir 512 KiB, o log anterior é preservado como
`mfg-updater.previous.log`. Falhas ao criar o log nunca bloqueiam o aplicativo.

A tela prioriza a biblioteca, o jogo selecionado e a ação de adicionar ao menu
HOME. A leitura do manifesto e a verificação de integridade continuam automáticas,
sem expor detalhes técnicos no fluxo normal.

O instalador é deliberadamente limitado aos forwarders plaintext produzidos pelo
MFG. Ele não é um instalador de NSP de uso geral.

O ícone oficial é incorporado de `icon.jpg` ao NACP do NRO.

## Dependências de compilação

- devkitPro;
- devkitA64 e libnx;
- portlib `switch-libjson-c`.
- portlibs `switch-glfw`, `switch-mesa` e `switch-glm`;
- Borealis, incluída em `lib/borealis`.

Com o ambiente devkitPro carregado, execute `make`. Depois copie `MFG-Updater.nro`
para `SD:/switch/MFG-Updater/MFG-Updater.nro`.
