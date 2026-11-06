# Architecture

[Português](Architecture.md) | **English**

## Product principle

MFG's architecture exists to lower the technical barrier around Sunshine and Moonlight. Implementation decisions should favor safe automation, plain language, actionable diagnostics, and a flow that requires neither programming knowledge nor manual configuration editing.

## Overview

MFG consists of a Windows companion and a Nintendo Switch homebrew companion.

```text
┌──────────────────────────────── PC ────────────────────────────────────┐
│ MFG.App → Sunshine → game                                             │
│    │                                                                  │
│    ├─ SQLite library                                                  │
│    ├─ local NSP generator                                             │
│    └─ verified SD card synchronization                                │
└───────────────────────────────┬────────────────────────────────────────┘
                                │ SD:/switch/MFG
┌───────────────────────────────▼ Switch ────────────────────────────────┐
│ MFG Updater → HOME shortcut → Moonlight-Switch → game stream         │
└────────────────────────────────────────────────────────────────────────┘
```

## Components

### MFG.App

A .NET 8 WPF application and the single interaction point on Windows. It handles onboarding, game discovery, the library, Sunshine setup, forwarder generation, and SD synchronization.

### MFG.Common

Models and rules that do not depend on the UI, including library records, generation results, and the testable onboarding flow.

### NSP generator

A Node.js backend distributed with the app. It receives a validated plan and produces a forwarder with a unique Title ID and Moonlight arguments. The key is decrypted only inside a random temporary directory and deleted afterward.

### MFG Updater

A C++ homebrew application based on libnx and Borealis. It reads the SD manifest, verifies SHA-256, validates the allowed NSP structure, and adds the selected forwarder to the HOME menu after confirmation.

## Persistence and files

Windows data is stored under `%LocalAppData%\MoonlightForwarderGenerator`:

| Content | Location |
| --- | --- |
| Library | `mfg.db` |
| Onboarding state | `setup.json` |
| Selected language | `language.json` |
| Protected key | `secrets/prod.keys.protected` |
| Generated NSPs | `forwarders/` |
| Logs | `logs/` |

SD card layout:

| Content | Location |
| --- | --- |
| Moonlight-Switch | `SD:/switch/Moonlight-Switch/` |
| Synchronized library | `SD:/switch/MFG/` |
| MFG Updater | `SD:/switch/MFG-Updater/MFG-Updater.nro` |

## Game flow

1. The user selects an executable.
2. The Companion creates or updates its Sunshine application.
3. The paired Moonlight connection resolves the application identifier.
4. The generator creates an NSP with a title, icon, Title ID, and launch arguments.
5. The Companion stores it locally and copies it to the SD card with SHA-256 verification.
6. Manifest v2 describes the desired library and covers.
7. MFG Updater validates and installs the selected forwarder.

## Security principles

- preserve Sunshine and Moonlight settings not owned by MFG;
- write important files through temporary paths and atomic replacement;
- verify removable-media copies by hash;
- prevent manifest paths from escaping the managed directory;
- never send keys to the Switch or include them in an NSP;
- accept only the restricted format produced by MFG in the Updater.

## Localization

Supported languages are defined under `MFG.Common.Localization`. The Companion stores the selected culture and loads its `ResourceDictionary` before creating the main window. Portuguese and English use identical keys; every new localizable string must be added to both dictionaries.
