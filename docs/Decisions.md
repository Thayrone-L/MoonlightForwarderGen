# Decisões

- O MVP é um único Companion WPF; não existe processo Agent.
- O Switch abre diretamente um favorito do Moonlight-Switch pelo forwarder NSP.
- A biblioteca armazena o ID do favorito Moonlight-Switch, não um suposto AppID permanente do Sunshine.
- O Companion não armazena `prod.keys` e não instala NSPs no console.
- A cópia opcional para o cartão SD usa `SD:\switch\`; sem cartão detectado, o NSP permanece na biblioteca local.
