# MFG Updater for Nintendo Switch

[Português](README.md) | **English**

Homebrew responsible for applying the forwarder library prepared by Moonlight Forwarder Generator to the console.

## Current milestone

Version `0.5.4` provides a Borealis-based accelerated graphical interface aligned with the computer application. It:

- opens `SD:/switch/MFG/manifest.json`;
- accepts manifest contracts v1 and v2;
- rejects filenames containing directory traversal;
- verifies each NSP's full SHA-256 directly on the SD card;
- lists forwarders, missing files, and requested removals;
- installs a valid selected forwarder after confirmation;
- accepts only PFS0 NSPs containing NCAs, without tickets, certificates, or extra files;
- verifies that CNMT describes an application and matches the manifest Title ID;
- registers content through NCM and publishes the application on the HOME menu;
- does not remove titles yet.

When an NSP and its manifest hash differ, the NRO blocks installation and asks the user to synchronize through the computer again. Synchronization recopies the file, flushes it to physical storage, checks SHA-256 again, and only then publishes the manifest.

## Switch logs

Persistent diagnostics are written to `SD:/switch/MFG/logs/mfg-updater.log`. The previous log is preserved as `mfg-updater.previous.log` once it reaches 512 KiB. Logging failures never block the app.

The screen prioritizes the library, selected game, and **Adicionar ao menu HOME (Add to HOME menu)** action. Manifest loading and integrity verification remain automatic without exposing technical details in normal use.

The installer is deliberately restricted to plaintext forwarders produced by MFG. It is not a general-purpose NSP installer.

## Build dependencies

- devkitPro;
- devkitA64 and libnx;
- `switch-libjson-c`;
- `switch-glfw`, `switch-mesa`, and `switch-glm`;
- Borealis, included under `lib/borealis`.

With the devkitPro environment loaded, run `make`. Then copy `MFG-Updater.nro` to `SD:/switch/MFG-Updater/MFG-Updater.nro`.
