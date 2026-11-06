# Arquitetura

**Português** | [English](Architecture.en.md)

## Princípio de produto

A arquitetura do MFG existe para reduzir a barreira técnica do Sunshine e do Moonlight. Decisões de implementação devem favorecer automação segura, linguagem simples, diagnóstico acionável e um fluxo que possa ser concluído sem conhecimento de programação ou edição manual de configurações.

## Visão geral

O MFG é formado por um Companion Windows e um complemento para Nintendo Switch.

```text
┌────────────────────────────── Computador ──────────────────────────────┐
│ MFG.App → Sunshine → jogo                                              │
│    │                                                                   │
│    ├─ biblioteca SQLite                                                │
│    ├─ gerador NSP local                                                │
│    └─ sincronização verificada com o cartão SD                         │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ SD:/switch/MFG
┌───────────────────────────────▼ Switch ─────────────────────────────────┐
│ MFG Updater → atalho no menu HOME → Moonlight-Switch → stream do jogo │
└─────────────────────────────────────────────────────────────────────────┘
```

## Componentes

### MFG.App

Aplicativo WPF em .NET 8 e ponto único de interação no Windows. Ele conduz o onboarding, descobre jogos, gerencia a biblioteca, prepara o Sunshine, gera os forwarders e sincroniza o cartão SD.

### MFG.Common

Contém modelos e regras sem dependência da interface, como registros da biblioteca, resultados de geração e o fluxo testável do onboarding.

### Gerador NSP

Backend Node.js distribuído junto ao aplicativo. Recebe um plano validado do Companion, usa template próprio e gera um forwarder com Title ID único e argumentos do Moonlight. A chave é descriptografada somente em uma pasta temporária aleatória e removida ao final.

### MFG Updater

Homebrew C++ baseado em libnx e Borealis. Lê o manifesto do cartão, verifica SHA-256, valida a estrutura permitida do NSP e adiciona o forwarder ao menu HOME após confirmação.

## Persistência e arquivos

No Windows, os dados ficam em `%LocalAppData%\MoonlightForwarderGenerator`:

| Conteúdo | Local |
| --- | --- |
| Biblioteca | `mfg.db` |
| Estado do onboarding | `setup.json` |
| Idioma escolhido | `language.json` |
| Chave protegida | `secrets/prod.keys.protected` |
| NSPs gerados | `forwarders/` |
| Logs | `logs/` |

No cartão SD:

| Conteúdo | Local |
| --- | --- |
| Moonlight-Switch | `SD:/switch/Moonlight-Switch/` |
| Biblioteca sincronizada | `SD:/switch/MFG/` |
| MFG Updater | `SD:/switch/MFG-Updater/MFG-Updater.nro` |

## Fluxo de um jogo

1. O usuário escolhe um executável.
2. O Companion cria ou atualiza a aplicação correspondente no Sunshine.
3. O identificador da aplicação é resolvido através do Moonlight pareado.
4. O gerador cria o NSP com nome, ícone, Title ID e argumentos necessários.
5. O Companion salva o NSP localmente e o copia para o SD com verificação SHA-256.
6. O manifesto v2 descreve a biblioteca desejada e as capas.
7. O MFG Updater valida e instala o forwarder selecionado.

## Princípios de segurança

- configurações alheias no Sunshine e Moonlight devem ser preservadas;
- gravações importantes usam arquivo temporário e substituição atômica;
- arquivos copiados para mídia removível são conferidos por hash;
- caminhos vindos do manifesto não podem escapar da pasta gerenciada;
- chaves nunca são enviadas ao Switch ou incluídas nos NSPs;
- o Updater aceita apenas o formato restrito produzido pelo MFG.

## Localização

Idiomas suportados são definidos em `MFG.Common.Localization`. O Companion salva a cultura escolhida e carrega um `ResourceDictionary` correspondente antes de criar a janela principal. Português e inglês usam as mesmas chaves; novos textos localizáveis devem ser adicionados aos dois dicionários.
