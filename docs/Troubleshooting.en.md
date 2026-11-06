# Troubleshooting

[Português](Troubleshooting.md) | **English**

Start by closing and reopening MFG. On the main screen, use **Configurações (Settings)** to resume setup and **Atualizar (Update)** to synchronize the SD card again.

## The application does not open

- Extract the entire ZIP; do not run `MFG.App.exe` from inside it.
- Confirm that the download came from the official Releases page.
- In SmartScreen, use **More info → Run anyway** only after that confirmation.
- Do not remove `node.exe`, `nsp-generator`, or `switch` from the package.

## Sunshine does not become ready

1. Wait a few seconds after installation.
2. Accept the Windows administrator prompt.
3. Open **Configurações → Preparar computador (Settings → Prepare computer)** again.
4. Check whether the firewall or antivirus blocked Sunshine.
5. Confirm that `https://localhost:47990` opens on the computer.

## The key is rejected

- Select a non-empty `.keys` file extracted from your own console.
- Do not rename an unrelated file to `prod.keys`.
- Run MFG as the same Windows user who imported it; protection is tied to that user.

## The SD card is not detected

- Confirm that Windows assigned it a drive letter.
- Check for `SD:/switch/Moonlight-Switch/`.
- Pairing requires `SD:/switch/Moonlight-Switch/settings.json`.
- Reconnect the card, then click **Verificar cartão SD (Check SD card)** or **Atualizar (Update)**.

## MFG Updater is missing

Connect the card to the computer and click **Atualizar (Update)**. Confirm that this file exists:

```text
SD:/switch/MFG-Updater/MFG-Updater.nro
```

If it exists, reopen the Homebrew Menu.

## A game is missing from the Updater

Check the SD card for:

```text
SD:/switch/MFG/manifest.json
SD:/switch/MFG/MFG - Game title.nsp
```

On the computer, select the game and click **Adicionar ao Switch (Add to Switch)**, then click **Atualizar (Update)** with the SD card connected.

## Invalid integrity

Do not install the reported file. Reconnect the card to the computer and synchronize again. MFG recalculates SHA-256 after copying.

## The shortcut opens Moonlight, but not the game

- First test whether Moonlight-Switch can open the computer normally.
- Make sure Sunshine is running.
- Reopen MFG and run the library check.
- If the game moved or was reinstalled, remove the old entry and add the correct executable.

## Logs for bug reports

On Windows, click **•••** in the top-right corner to open the log directory, or browse to:

```text
%LocalAppData%\MoonlightForwarderGenerator\logs
```

On the Switch:

```text
SD:/switch/MFG/logs/mfg-updater.log
```

Review logs before publishing them and remove personal paths or names. Never publish `prod.keys`.

When opening an issue, include:

1. MFG version;
2. Windows version;
3. failing stage;
4. displayed message;
5. reproduction steps;
6. relevant log excerpt.
