# Arquitetura

O MFG é um único Companion WPF. Não há Agent nem comunicação por named pipe.

```text
Switch → Forwarder NSP → Moonlight-Switch → Sunshine → Executável
```

O Companion gerencia Sunshine, biblioteca local, favoritos do Moonlight-Switch, geração de NSP e health checks. O identificador usado pelo NSP é o favorito do Moonlight-Switch associado à aplicação do Sunshine.

Ao registrar um jogo, o Companion cria ou atualiza sua aplicação no `apps.json` do Sunshine. A entrada mantém o nome escolhido pelo usuário, o executável e seu diretório de trabalho; demais aplicações são preservadas.

O Companion detecta um cartão SD removível que contenha `switch\Moonlight-Switch\settings.json`. Essa detecção é o pré-requisito para a sincronização dos favoritos e a cópia futura dos NSPs.

O módulo NSP reserva um Title ID único por jogo e monta os argumentos `--ip`, `--appid` e `--appname`. A construção binária usa um template e um backend externo compatível, isolados da biblioteca e da interface.
