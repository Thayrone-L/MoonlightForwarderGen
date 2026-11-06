# Desenvolvimento

**Português** | [English](Development.en.md)

## Tecnologias

| Área | Tecnologia |
| --- | --- |
| Companion Windows | C#, .NET 8, WPF |
| Persistência | SQLite, Entity Framework Core |
| Gerador NSP | Node.js |
| Complemento Switch | C++, libnx, Borealis |
| Testes | xUnit |
| Automação | GitHub Actions, PowerShell |

## Estrutura do repositório

```text
src/MFG.App/              Aplicativo Windows e integrações
src/MFG.Common/           Modelos e regras independentes da interface
tests/MFG.Common.Tests/   Testes unitários atuais
tools/nsp-generator/      Backend e template dos forwarders
switch/MFG.Updater/       Complemento para Nintendo Switch
scripts/                  Empacotamento de releases
docs/                     Arquitetura e guias
```

## Compilar e executar

Requisitos para o Companion:

- Windows;
- SDK .NET 8;
- Node.js 24 para geração local de NSP.

```powershell
npm ci --prefix tools/nsp-generator
dotnet restore MoonlightForwarderGenerator.sln
dotnet build MoonlightForwarderGenerator.sln
dotnet run --project src/MFG.App
```

O binário `switch/MFG.Updater/MFG-Updater.nro` é distribuído com o Companion. Consulte [SwitchUpdater.md](SwitchUpdater.md) para recompilá-lo.

## Testes

```powershell
dotnet test MoonlightForwarderGenerator.sln
```

A suíte atual cobre as regras do fluxo de onboarding. A expansão prioritária está registrada no [Roadmap](Roadmap.md).

## Localização

Os idiomas ficam em `src/MFG.App/Localization/Strings.pt-BR.xaml` e `Strings.en.xaml`. Use `DynamicResource` no XAML e `LocalizedText.Get` ou `LocalizedText.Format` no código. Toda nova chave deve existir nos dois arquivos. Os códigos suportados e sua normalização ficam em `MFG.Common.Localization.AppLanguage`.

## Gerar um pacote local

```powershell
./scripts/Build-Release.ps1 -Version 0.1.0-alpha.1
```

O script executa os testes, publica uma aplicação Windows x64 autossuficiente, inclui Node.js e gera ZIP e SHA-256 em `artifacts/release`.

## Publicar uma release

O workflow `.github/workflows/release.yml` é acionado por tags `v*`:

```powershell
git tag -a v0.1.0-alpha.1 -m "Moonlight Forwarder Generator v0.1.0-alpha.1"
git push origin v0.1.0-alpha.1
```

O GitHub Actions gera o pacote, cria a release como prerelease e anexa ZIP e checksum. A tag deve apontar para um commit em que testes e CI estejam verdes.

## Princípios para contribuições

- preservar configurações que não pertencem ao MFG;
- não registrar chaves ou dados sensíveis em logs;
- validar caminhos e conteúdo antes de gravar ou instalar;
- manter regras testáveis fora do code-behind quando possível;
- atualizar documentação e testes junto com mudanças de comportamento.
