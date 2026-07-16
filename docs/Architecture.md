# Arquitetura

O MVP separa UI, comunicação compartilhada e execução local. O `MFG.App` envia comandos pelo named pipe local ao `MFG.Agent`, que valida o comando e inicia o processo correspondente.

Na inicialização, o Agent detecta o Sunshine e mantém a tabela `SunshineInstallations` sincronizada com o caminho atual do executável. Quando instalado e ainda inativo, o Agent inicia o processo e espera pela disponibilidade local das portas TCP padrão de streaming (`47989`) e da interface web (`47990`).

Quando a App solicita `get-sunshine-status`, o Agent atualiza e retorna esse estado. Para `install-sunshine`, o Agent consulta a API oficial de releases do GitHub, baixa apenas o instalador Windows compatível da release estável mais recente, abre-o de forma interativa e executa novamente a detecção ao término.

No próximo estágio, `MFG.Protocol` definirá os comandos recebidos pela cadeia Switch → Moonlight → Sunshine, e `MFG.Core` centralizará a resolução de jogos.
