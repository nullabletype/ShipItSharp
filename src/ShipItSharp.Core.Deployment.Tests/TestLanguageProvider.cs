using NSubstitute;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Core.Deployment.Tests;

internal static class TestLanguageProvider
{
    public static ILanguageProvider Create()
    {
        var language = Substitute.For<ILanguageProvider>();
        language.GetString(Arg.Any<LanguageSection>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1));
        return language;
    }
}
