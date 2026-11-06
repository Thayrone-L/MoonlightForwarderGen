using MFG.Common.Localization;
using Xunit;

namespace MFG.Common.Tests.Localization;

public sealed class AppLanguageTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("PT-br")]
    [InlineData("en")]
    [InlineData("EN")]
    public void RecognizesSupportedLanguages(string cultureName)
    {
        Assert.True(AppLanguage.IsSupported(cultureName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pt")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void FallsBackToPortugueseForUnsupportedLanguages(string? cultureName)
    {
        Assert.Equal(AppLanguage.Portuguese, AppLanguage.Normalize(cultureName));
    }

    [Fact]
    public void NormalizesSupportedLanguageCasing()
    {
        Assert.Equal(AppLanguage.Portuguese, AppLanguage.Normalize("PT-br"));
        Assert.Equal(AppLanguage.English, AppLanguage.Normalize("EN"));
    }
}
