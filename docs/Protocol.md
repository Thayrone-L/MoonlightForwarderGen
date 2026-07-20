# Contratos de integração

O MFG não possui protocolo interno de comandos. A integração é feita por arquivos e argumentos:

| Componente | Contrato |
| --- | --- |
| Sunshine | Aplicação registrada no `apps.json` ou pela Web UI. |
| Moonlight-Switch | Favorito em `SD:\switch\Moonlight-Switch\settings.json`. |
| Forwarder NSP | Argumentos `--ip`, `--appid` e `--appname`. |
| Biblioteca MFG | UUID interno, executável, aplicação Sunshine, ID do favorito e caminho do NSP. |

O Companion deve preservar configurações e favoritos que não foram criados por ele.
