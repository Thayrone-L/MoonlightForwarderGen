# Moonlight Forwarder Generator — contexto do MVP

## Objetivo

O MFG é um Companion Windows para preparar jogos de PC para abertura direta pelo menu HOME de um Nintendo Switch com CFW:

```text
Forwarder NSP → Moonlight-Switch → Sunshine → Executável
```

Não há Agent, named pipe ou launcher intermediário no MVP.

## Responsabilidades do Companion

- **Sunshine Manager**: instalar, validar, iniciar e configurar o Sunshine.
- **Library Manager**: manter nome, executável, aplicação Sunshine, favorito Moonlight, NSP e status de cada jogo.
- **NSP Generator**: criar o NSP com os parâmetros do favorito Moonlight-Switch e copiá-lo para o cartão SD quando disponível.

## Fluxo do usuário

1. Selecionar um executável.
2. O Companion prepara a aplicação correspondente no Sunshine.
3. O Companion cria ou sincroniza o favorito do Moonlight-Switch.
4. O Companion gera o NSP.
5. Com o SD conectado, copia a saída para `SD:\switch\`; caso contrário, salva localmente.

## Identificadores

O identificador usado pelo forwarder é o ID do favorito do Moonlight-Switch, associado a uma aplicação do Sunshine. O UUID interno da biblioteca existe apenas para o Companion.

## Health check

Ao iniciar, o Companion verifica Sunshine instalado/em execução, executáveis existentes, aplicações Sunshine, favoritos Moonlight-Switch e NSPs gerados. A interface usa linguagem simples, por exemplo “Preparando jogo”, sem expor esses detalhes técnicos.
