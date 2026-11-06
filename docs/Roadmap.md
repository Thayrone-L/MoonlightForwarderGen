# Roadmap

**Português** | [English](Roadmap.en.md)

## Entregue na alpha atual

- Companion WPF com biblioteca visual.
- Descoberta de jogos Steam, Epic e executáveis locais.
- Instalação, configuração e health check do Sunshine.
- Onboarding retomável.
- Seleção persistente de idioma e onboarding em português e inglês.
- Armazenamento protegido de `prod.keys`.
- Geração de forwarders NSP com nome, capa e Title ID único.
- Sincronização verificada com o cartão SD.
- MFG Updater com interface gráfica e instalação no menu HOME.
- Testes iniciais, CI e pipeline automatizado de releases.

## Próximas versões alpha

1. Ampliar testes unitários dos serviços e contratos do manifesto.
2. Concluir a internacionalização da tela principal, mensagens de serviços e MFG Updater.
3. Melhorar mensagens de recuperação e diagnóstico dentro da interface.
4. Validar o fluxo completo em diferentes versões do Windows, Sunshine e Moonlight-Switch.
5. Adicionar telemetria somente local de duração e falhas para diagnóstico, sem envio externo.
6. Implementar remoção opt-in de forwarders pelo MFG Updater.

## Antes da beta

- instalador Windows com desinstalação limpa;
- assinatura de código ou documentação formal de hashes;
- migrações explícitas de banco e configuração;
- testes de integração do gerador e do cartão SD;
- guia visual com screenshots e vídeo curto;
- matriz pública de compatibilidade validada em hardware.

## Ideias posteriores

- bibliotecas GOG, Playnite e Xbox;
- atualização automática do Companion;
- Wake-on-LAN;
- automações de início e encerramento da sessão de jogo.
