# Integration contracts

[Português](Protocol.md) | **English**

MFG has no internal network protocol. Components communicate through explicit settings, files, and arguments.

| Component | Contract |
| --- | --- |
| Sunshine | Application preserved or registered in `apps.json`. |
| Moonlight-Switch | Paired host in `SD:/switch/Moonlight-Switch/settings.json`. |
| Forwarder NSP | `--ip`, `--appid`, and `--appname` arguments. |
| MFG library | UUID, executable, Sunshine application, Moonlight ID, Title ID, cover, and NSP. |
| MFG Updater | `SD:/switch/MFG/manifest.json`. |

## SD card manifest

The Companion currently writes `schemaVersion: 2`. The Updater accepts versions 1 and 2 for compatibility.

| Field | Purpose |
| --- | --- |
| `schemaVersion` | Contract version. |
| `updatedAt` | UTC timestamp of the last synchronization. |
| `games` | Desired Switch library. |
| `games[].titleId` | Title ID reserved for the forwarder. |
| `games[].name` | User-facing title. |
| `games[].nspFileName` | NSP inside `SD:/switch/MFG/`. |
| `games[].sha256` | Hash used to validate the copy. |
| `games[].coverFileName` | Optional cover under `covers/`, introduced in v2. |
| `removedTitleIds` | Items removed from the library; console removal is not implemented yet. |

## Invariants

- paths cannot contain directory traversal;
- the NSP hash must match the manifest;
- the package Title ID must match the manifest;
- settings and applications not managed by MFG must be preserved;
- an invalid manifest must never cause a silent partial installation.
