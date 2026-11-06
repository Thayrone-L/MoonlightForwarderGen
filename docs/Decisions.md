# Decisões técnicas

**Português** | [English](Decisions.en.md)

## Companion único no Windows

O aplicativo WPF concentra a configuração, a biblioteca e as integrações do computador. Essa abordagem simplifica a instalação e reduz processos em segundo plano e pontos de falha.

## Integração por contratos existentes

O MFG integra-se ao Sunshine por sua configuração de aplicações e ao Moonlight-Switch pelo host pareado. O projeto preserva entradas que não foram criadas por ele.

## Biblioteca local em SQLite

SQLite mantém identidade, executável, aplicação Sunshine, Title ID, capa, NSP e estado de cada jogo sem exigir servidor ou conta externa.

## Chave protegida no perfil do Windows

O arquivo importado é criptografado com DPAPI para o usuário atual. A versão em texto puro existe apenas em memória e em uma pasta temporária durante a geração, sendo zerada ou removida ao final.

## Entrega pelo cartão SD

O cartão SD é o limite explícito entre Companion e console. O MFG copia NSPs, capas, manifesto e o MFG Updater; não tenta controlar o Switch pela rede.

## Instalador restrito no Switch

O MFG Updater não é um instalador genérico. Ele aceita apenas forwarders plaintext no formato produzido pelo projeto, confere hash, Title ID, CNMT e estrutura antes de registrar conteúdo.

## Operações recuperáveis

Configurações e arquivos importantes são escritos primeiro em caminhos temporários. A substituição acontece somente após a gravação completa e, no SD, após nova verificação de integridade.

## Release autossuficiente

O pacote Windows x64 inclui o runtime .NET, o backend Node.js, suas dependências e o binário do MFG Updater. O usuário final não precisa preparar um ambiente de desenvolvimento.
