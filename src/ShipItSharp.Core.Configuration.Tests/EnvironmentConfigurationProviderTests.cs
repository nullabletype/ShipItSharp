using System;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Configuration.Interfaces;

namespace ShipItSharp.Core.Configuration.Tests;

[TestFixture]
public class EnvironmentConfigurationProviderTests
{
    [SetUp]
    [TearDown]
    public void ClearEnvironment()
    {
        Clear(EnvironmentConfigurationProvider.OctopusUrlEnvironmentVariable);
        Clear(EnvironmentConfigurationProvider.ApiKeyEnvironmentVariable);
        Clear(EnvironmentConfigurationProvider.DefaultChannelEnvironmentVariable);
        Clear(EnvironmentConfigurationProvider.CacheTimeoutEnvironmentVariable);
        Clear(EnvironmentConfigurationProvider.CheckForBetaReleasesEnvironmentVariable);
    }

    [Test]
    public async Task LoadConfiguration_UsesEnvironmentValues_AndConnectivityValidator()
    {
        IConfiguration validatedConfig = null;
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        validator.ValidateConnectivity(Arg.Do<IConfiguration>(config => validatedConfig = config))
            .Returns(Task.CompletedTask);
        Set(EnvironmentConfigurationProvider.OctopusUrlEnvironmentVariable, "https://octopus.example");
        Set(EnvironmentConfigurationProvider.ApiKeyEnvironmentVariable, "API-123");
        Set(EnvironmentConfigurationProvider.DefaultChannelEnvironmentVariable, "Main");
        Set(EnvironmentConfigurationProvider.CacheTimeoutEnvironmentVariable, "42");
        Set(EnvironmentConfigurationProvider.CheckForBetaReleasesEnvironmentVariable, "true");
        var provider = new EnvironmentConfigurationProvider(TestLanguageProvider.Create(), validator);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Configuration.OctopusUrl, Is.EqualTo("https://octopus.example"));
        Assert.That(result.Configuration.ApiKey, Is.EqualTo("API-123"));
        Assert.That(result.Configuration.DefaultChannel, Is.EqualTo("Main"));
        Assert.That(result.Configuration.CacheTimeoutInSeconds, Is.EqualTo(42));
        Assert.That(result.Configuration.CheckForBetaReleases, Is.True);
        Assert.That(validatedConfig, Is.Not.Null);
    }

    [Test]
    public async Task LoadConfiguration_ReturnsRequiredShapeErrors_BeforeConnectivity()
    {
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        Set(EnvironmentConfigurationProvider.OctopusUrlEnvironmentVariable, "https://octopus.example");
        var provider = new EnvironmentConfigurationProvider(TestLanguageProvider.Create(), validator);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Does.Contain("ValidationOctopusApiKey"));
        await validator.DidNotReceiveWithAnyArgs().ValidateConnectivity(default);
    }

    [Test]
    public async Task LoadConfiguration_UsesDefaultChannelAndCacheTimeoutDefaults()
    {
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        validator.ValidateConnectivity(Arg.Any<IConfiguration>()).Returns(Task.CompletedTask);
        Set(EnvironmentConfigurationProvider.OctopusUrlEnvironmentVariable, "https://octopus.example");
        Set(EnvironmentConfigurationProvider.ApiKeyEnvironmentVariable, "API-123");
        var provider = new EnvironmentConfigurationProvider(TestLanguageProvider.Create(), validator);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Configuration.DefaultChannel, Is.EqualTo("Default"));
        Assert.That(result.Configuration.CacheTimeoutInSeconds, Is.EqualTo(1));
        Assert.That(result.Configuration.CheckForBetaReleases, Is.False);
    }

    [Test]
    public async Task ConfigurationProvider_PrefersEnvironmentConfiguration_WhenEnvironmentVariablesArePresent()
    {
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        validator.ValidateConnectivity(Arg.Any<IConfiguration>()).Returns(Task.CompletedTask);
        Set(EnvironmentConfigurationProvider.OctopusUrlEnvironmentVariable, "https://octopus.example");
        Set(EnvironmentConfigurationProvider.ApiKeyEnvironmentVariable, "API-123");

        var result = await ConfigurationProvider.LoadConfiguration(ConfigurationProviderTypes.Json, TestLanguageProvider.Create(), validator);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Configuration.OctopusUrl, Is.EqualTo("https://octopus.example"));
        Assert.That(result.Configuration.ApiKey, Is.EqualTo("API-123"));
    }

    private static void Set(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }

    private static void Clear(string name)
    {
        Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
    }
}
