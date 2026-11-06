# Moonlight Forwarder Generator — current context

[Português](MOONLIGHT_FORWARDER_GENERATOR_BRIEF.md) | **English**

## Product

MFG lets users open PC games from the HOME menu of a Nintendo Switch already prepared for homebrew:

```text
HOME shortcut → Moonlight-Switch → Sunshine → PC game
```

Its goal is to democratize Sunshine and Moonlight by replacing manual configuration and technical knowledge with safe automation, clear guidance, and an accessible visual experience.

## Components

- `MFG.App`: WPF Companion for onboarding, library, Sunshine, NSP generation, and SD synchronization.
- `MFG.Common`: UI-independent models and rules.
- `tools/nsp-generator`: Node.js backend and forwarder template.
- `switch/MFG.Updater`: homebrew that validates and installs forwarders on the HOME menu.

## Current state

- Steam, Epic, and local executable discovery.
- Sunshine installation and preparation with user consent.
- DPAPI-protected key under the current Windows user.
- NSP with a unique Title ID, title, cover, and Moonlight arguments.
- SHA-256-verified SD copies and manifest v2.
- Graphical MFG Updater restricted to the project format.
- Resumable onboarding, initial tests, CI, and a self-contained alpha release.

## Priorities

1. Expand service and integration tests.
2. Validate the alpha across environments and hardware.
3. Improve diagnostics and error recovery.
4. Prepare installer, signing, and compatibility work for beta.

See `README.en.md` for the user journey and `docs/README.en.md` for the technical index.
