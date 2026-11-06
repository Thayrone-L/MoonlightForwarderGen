# MFG Updater for Nintendo Switch

[Português](SwitchUpdater.md) | **English**

## Product role

MFG Updater adds computer-generated forwarders to the HOME menu. The Companion automatically copies it to `SD:/switch/MFG-Updater/MFG-Updater.nro` when an SD card is detected.

The current component version is `0.5.4`. It is versioned independently from the Windows package, currently `0.1.0-alpha.1`.

## User flow

1. Synchronize the library through MFG on the computer.
2. Insert the SD card into the Switch.
3. Open MFG Updater through the Homebrew Menu.
4. Select a game with valid integrity.
5. Confirm **Adicionar ao menu HOME (Add to HOME menu)**.

## Security boundaries

The Updater:

- reads only `SD:/switch/MFG/manifest.json`;
- accepts manifest contracts v1 and v2;
- rejects directory traversal;
- verifies the full NSP SHA-256;
- accepts only PFS0 NSPs containing NCAs, without tickets or certificates;
- verifies content type, CNMT, and Title ID;
- installs only after user confirmation;
- does not remove installed titles yet.

It is deliberately restricted to plaintext forwarders produced by MFG and is not a general-purpose NSP installer.

## Switch logs

Diagnostics are stored in `SD:/switch/MFG/logs/mfg-updater.log`. At 512 KiB, the previous file is preserved as `mfg-updater.previous.log`. Logging failures never block the app.

## Build

The build requires devkitPro, devkitA64, libnx, `switch-libjson-c`, `switch-glfw`, `switch-mesa`, and `switch-glm`. Borealis is included under `switch/MFG.Updater/lib/borealis`.

```bash
cd switch/MFG.Updater
make
```
