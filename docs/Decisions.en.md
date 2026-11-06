# Technical decisions

[Português](Decisions.md) | **English**

## Single Windows companion

The WPF app centralizes computer setup, the library, and integrations. This simplifies installation and reduces background processes and failure points.

## Integration through existing contracts

MFG integrates with Sunshine through its application configuration and with Moonlight-Switch through its paired host. It preserves entries it did not create.

## Local SQLite library

SQLite stores identity, executable, Sunshine application, Title ID, cover, NSP, and game state without requiring a server or external account.

## Key protected by the Windows profile

The imported file is encrypted with DPAPI for the current user. Plaintext exists only in memory and in a temporary directory during generation and is cleared or deleted afterward.

## SD card delivery boundary

The SD card is the explicit boundary between the Companion and console. MFG copies NSPs, covers, the manifest, and MFG Updater instead of attempting remote console control.

## Restricted Switch installer

MFG Updater is not a generic installer. It accepts only the plaintext forwarder format created by this project and verifies its hash, Title ID, CNMT, and structure.

## Recoverable operations

Important files and settings are first written to temporary paths. Replacement happens only after writing completes and, on the SD card, after integrity is checked again.

## Self-contained release

The Windows x64 package includes .NET, Node.js, generator dependencies, and the MFG Updater binary. End users do not need a development environment.
