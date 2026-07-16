# Moonlight Forwarder Generator

MVP para iniciar aplicações no PC a partir de comandos recebidos por um agente local. A arquitetura final integrará forwarders de Nintendo Switch, Moonlight e Sunshine.

## MVP atual

- `MFG.Agent`: processo em segundo plano que escuta comandos em um named pipe local e persiste jogos em SQLite.
- `MFG.App`: interface WPF para cadastrar um ID e executável, ou iniciar um jogo já cadastrado pelo ID.
- `MFG.Common`: contratos e cliente de comunicação compartilhados.

## Executar a prova de conceito

Em dois terminais, na raiz da solução:

```powershell
dotnet run --project src/MFG.Agent
dotnet run --project src/MFG.App
```

Cadastre um ID (por exemplo, `notepad`) e o caminho de um executável. Em seguida, informe o mesmo ID na seção de execução. Os dados são armazenados em `%LocalAppData%\MoonlightForwarderGenerator\mfg.db`.

Ao iniciar, o Agent também verifica se o Sunshine está instalado. Ele procura nos diretórios de instalação usuais e no Registro do Windows e salva o estado e o caminho encontrado na tabela `SunshineInstallations` do mesmo banco.

## Estrutura planejada

Os projetos `MFG.Core`, `MFG.Protocol`, `MFG.NSPGenerator` e `MFG.Installer` serão adicionados após a validação do MVP. Os testes correspondentes também serão criados nessa etapa.
