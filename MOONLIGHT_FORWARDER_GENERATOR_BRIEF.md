# Moonlight Forwarder Generator — contexto atual

**Português** | [English](MOONLIGHT_FORWARDER_GENERATOR_BRIEF.en.md)

## Produto

O MFG permite abrir jogos de PC pelo menu HOME de um Nintendo Switch já preparado para homebrew:

```text
Atalho HOME → Moonlight-Switch → Sunshine → jogo no PC
```

Seu objetivo é democratizar o uso do Sunshine e do Moonlight, substituindo configurações manuais e conhecimento técnico por automação segura, orientação clara e uma experiência visual acessível.

## Componentes

- `MFG.App`: Companion WPF que conduz onboarding, biblioteca, Sunshine, geração de NSP e sincronização do SD.
- `MFG.Common`: modelos e regras independentes da interface.
- `tools/nsp-generator`: backend Node.js e template do forwarder.
- `switch/MFG.Updater`: homebrew que valida e instala os forwarders no menu HOME.

## Estado atual

- Descoberta Steam, Epic e executáveis locais.
- Sunshine instalado e preparado com consentimento do usuário.
- Chave protegida por DPAPI no usuário atual do Windows.
- NSP com Title ID único, nome, capa e argumentos Moonlight.
- Cópia para SD com verificação SHA-256 e manifesto v2.
- MFG Updater gráfico, restrito ao formato produzido pelo projeto.
- Onboarding retomável, testes iniciais, CI e release alpha autossuficiente.

## Prioridades

1. Expandir testes de serviços e integrações.
2. Validar a alpha em diferentes ambientes e hardware.
3. Melhorar diagnóstico e recuperação de erros.
4. Preparar instalador, assinatura e compatibilidade para beta.

Consulte `README.md` para a jornada do usuário e `docs/README.md` para o índice técnico.
