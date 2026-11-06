# Traduções e idiomas

**Português** | [English](Localization.en.md)

O MFG usa inglês como idioma-base e fallback obrigatório. Assim, uma tradução incompleta nunca remove um texto da interface. Atualmente, português do Brasil e inglês estão disponíveis.

## Como colaborar

1. Copie o catálogo inglês da interface desejada.
2. Traduza somente os valores; não altere chaves como `Main.AddGame`.
3. Preserve placeholders como `{0}` no Companion e `{}` no NRO.
4. Salve o arquivo em UTF-8.
5. Execute `./scripts/Validate-Localization.ps1` antes de enviar a contribuição.

Catálogos:

- Companion para Windows: `src/MFG.App/Localization/Strings.<idioma>.xaml`
- MFG Updater (NRO): `switch/MFG.Updater/lib/borealis/resources/i18n/<idioma>/mfg.json`

Um novo idioma do Companion também deve ser registrado em `AppLanguage.cs` e `LanguageSettingsStore.cs`. No NRO, use o código de idioma reconhecido pelo Borealis/libnx, por exemplo `de`, `ru`, `ja`, `zh-CN` ou `zh-TW`.

## Regras para não quebrar as telas

- Prefira frases curtas e naturais; não traduza palavra por palavra.
- Não dependa de largura fixa. Textos explicativos devem permitir quebra de linha.
- Confira botões e cabeçalhos com textos aproximadamente 30% maiores, uma situação comum em alemão.
- Confira caracteres cirílicos e CJK reais, não imagens ou letras transliteradas.
- Nunca coloque mensagens apresentadas ao usuário diretamente no código: crie uma chave no catálogo inglês e em cada tradução mantida pelo projeto.

O Companion usa o fallback de fontes do Windows. O NRO carrega as fontes compartilhadas do Switch para latim, cirílico, japonês, chinês simplificado e tradicional e coreano. A validação automática da CI compara chaves, valores vazios e placeholders.
