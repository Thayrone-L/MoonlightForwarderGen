# Complemento MFG Updater para Switch

## Objetivo

O MFG Updater aplica, no próprio Switch, os forwarders que o Companion gravou em
`SD:/switch/MFG`. O Companion continua responsável pela geração; o complemento
somente consome um plano explícito.

## Contrato v1

`manifest.json` contém:

| Campo | Função |
| --- | --- |
| `schemaVersion` | Versão do contrato; atualmente `1`. |
| `updatedAt` | Data UTC da última sincronização feita pelo Companion. |
| `games` | Forwarders desejados, com Title ID, nome, NSP e SHA-256. |
| `removedTitleIds` | Forwarders removidos da biblioteca e candidatos à desinstalação. |

O complemento nunca deve instalar arquivos fora de `SD:/switch/MFG`, aceitar
travessia de diretório ou remover um título que não esteja explicitamente em
`removedTitleIds`.

## Marcos

1. Leitura, validação e visualização do plano no hardware.
2. Verificação SHA-256 dos NSPs e persistência local do último plano aplicado.
3. Backend de instalação e atualização, com confirmação do usuário. Implementado
   inicialmente para NSPs plaintext do MFG e destino SD.
4. Remoções opt-in, resumo do resultado e recuperação de falhas parciais.

Os marcos 1 a 3 estão implementados em `switch/MFG.Updater`; a validação do marco
3 em hardware começa na versão experimental `0.3.0`.
