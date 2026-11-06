# Contratos de integração

**Português** | [English](Protocol.en.md)

O MFG não possui protocolo interno de rede. Os componentes comunicam-se por configurações, arquivos e argumentos explícitos.

| Componente | Contrato |
| --- | --- |
| Sunshine | Aplicação preservada ou registrada em `apps.json`. |
| Moonlight-Switch | Host pareado em `SD:/switch/Moonlight-Switch/settings.json`. |
| Forwarder NSP | Argumentos `--ip`, `--appid` e `--appname`. |
| Biblioteca MFG | UUID, executável, aplicação Sunshine, ID Moonlight, Title ID, capa e NSP. |
| MFG Updater | `SD:/switch/MFG/manifest.json`. |

## Manifesto do cartão SD

O Companion atualmente grava `schemaVersion: 2`. O Updater aceita versões 1 e 2 para manter compatibilidade.

| Campo | Função |
| --- | --- |
| `schemaVersion` | Versão do contrato. |
| `updatedAt` | Data UTC da última sincronização. |
| `games` | Biblioteca desejada no Switch. |
| `games[].titleId` | Title ID reservado para o forwarder. |
| `games[].name` | Nome exibido ao usuário. |
| `games[].nspFileName` | NSP dentro de `SD:/switch/MFG/`. |
| `games[].sha256` | Hash usado para validar a cópia. |
| `games[].coverFileName` | Capa opcional dentro de `covers/`; adicionado na v2. |
| `removedTitleIds` | Itens removidos da biblioteca; remoção no console ainda não é executada. |

## Invariantes

- caminhos não podem conter travessia de diretório;
- o hash do NSP deve coincidir com o manifesto;
- o Title ID interno do pacote deve coincidir com o manifesto;
- configurações e aplicações não gerenciadas pelo MFG devem ser preservadas;
- um manifesto inválido nunca deve causar instalação parcial silenciosa.
