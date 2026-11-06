# Translations and languages

[Português](Localization.md) | **English**

MFG uses English as its mandatory base language and fallback. An incomplete translation therefore never removes text from the interface. Brazilian Portuguese and English are currently available.

## How to contribute

1. Copy the English catalog for the target interface.
2. Translate values only; do not change keys such as `Main.AddGame`.
3. Preserve placeholders such as `{0}` in the Companion and `{}` in the NRO.
4. Save the file as UTF-8.
5. Run `./scripts/Validate-Localization.ps1` before submitting the contribution.

Catalogs:

- Windows Companion: `src/MFG.App/Localization/Strings.<language>.xaml`
- MFG Updater (NRO): `switch/MFG.Updater/lib/borealis/resources/i18n/<language>/mfg.json`

A new Companion language must also be registered in `AppLanguage.cs` and `LanguageSettingsStore.cs`. For the NRO, use a language code recognized by Borealis/libnx, such as `de`, `ru`, `ja`, `zh-CN`, or `zh-TW`.

## Rules that keep layouts safe

- Prefer short, natural sentences instead of literal translations.
- Do not rely on fixed widths. Explanatory text must be allowed to wrap.
- Check buttons and headers with text about 30% longer, a common German expansion case.
- Check real Cyrillic and CJK characters, not images or transliterations.
- Never place a user-facing message directly in code: add a key to the English catalog and every translation maintained by the project.

The Companion uses Windows font fallback. The NRO loads Switch shared fonts for Latin, Cyrillic, Japanese, simplified and traditional Chinese, and Korean. CI validation compares keys, empty values, and placeholders automatically.
