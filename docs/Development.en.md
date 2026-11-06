# Development

[Português](Development.md) | **English**

## Technology

| Area | Technology |
| --- | --- |
| Windows Companion | C#, .NET 8, WPF |
| Persistence | SQLite, Entity Framework Core |
| NSP generator | Node.js |
| Switch companion | C++, libnx, Borealis |
| Tests | xUnit |
| Automation | GitHub Actions, PowerShell |

## Repository structure

```text
src/MFG.App/              Windows application and integrations
src/MFG.Common/           UI-independent models and rules
tests/MFG.Common.Tests/   Current unit tests
tools/nsp-generator/      Forwarder backend and template
switch/MFG.Updater/       Nintendo Switch companion
scripts/                  Release packaging
docs/                     Architecture and guides
```

## Build and run

Companion requirements:

- Windows;
- .NET 8 SDK;
- Node.js 24 for local NSP generation.

```powershell
npm ci --prefix tools/nsp-generator
dotnet restore MoonlightForwarderGenerator.sln
dotnet build MoonlightForwarderGenerator.sln
dotnet run --project src/MFG.App
```

The `switch/MFG.Updater/MFG-Updater.nro` binary is distributed with the Companion. See [SwitchUpdater.en.md](SwitchUpdater.en.md) to rebuild it.

## Tests

```powershell
dotnet test MoonlightForwarderGenerator.sln
```

The current suite covers onboarding flow rules. Priority expansion is tracked in the [Roadmap](Roadmap.en.md).

## Localization

Languages live in `src/MFG.App/Localization/Strings.pt-BR.xaml` and `Strings.en.xaml`. Use `DynamicResource` in XAML and `LocalizedText.Get` or `LocalizedText.Format` in code. Every new key must exist in both files. Supported codes and normalization live in `MFG.Common.Localization.AppLanguage`.

## Build a local package

```powershell
./scripts/Build-Release.ps1 -Version 0.1.0-alpha.1
```

The script runs tests, publishes a self-contained Windows x64 application, includes Node.js, and generates a ZIP and SHA-256 file under `artifacts/release`.

## Publish a release

The `.github/workflows/release.yml` workflow runs for `v*` tags:

```powershell
git tag -a v0.1.0-alpha.1 -m "Moonlight Forwarder Generator v0.1.0-alpha.1"
git push origin v0.1.0-alpha.1
```

GitHub Actions builds the package, creates a prerelease, and attaches the ZIP and checksum. The tag should point to a commit with green tests and CI.

## Contribution principles

- preserve settings not owned by MFG;
- never write keys or sensitive data to logs;
- validate paths and content before writing or installing;
- keep rules outside code-behind when practical so they can be tested;
- update documentation and tests together with behavior changes.
