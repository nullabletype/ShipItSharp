using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Configuration.Interfaces;

namespace ShipItSharp.Core.Configuration.Tests;

[TestFixture]
public class JsonConfigurationProviderTests
{
    [Test]
    public async Task LoadConfiguration_CreatesSampleConfigAndReturnsFailure_WhenFileIsMissing()
    {
        var path = CreateTempPath();
        var provider = new JsonConfigurationProvider(TestLanguageProvider.Create(), Substitute.For<IConfigurationConnectivityValidator>(), path);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Does.Contain("LoadNoFileFound"));
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task LoadConfiguration_ReturnsParseError_WhenJsonIsInvalid()
    {
        var path = WriteTempFile("{ invalid json");
        var provider = new JsonConfigurationProvider(TestLanguageProvider.Create(), Substitute.For<IConfigurationConnectivityValidator>(), path);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Does.Contain("LoadCouldntParseFile"));
    }

    [Test]
    public async Task LoadConfiguration_ValidatesRequiredShape_BeforeConnectivity()
    {
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        var path = WriteTempFile(JsonConvert.SerializeObject(new { OctopusUrl = "", ApiKey = "" }));
        var provider = new JsonConfigurationProvider(TestLanguageProvider.Create(), validator, path);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Does.Contain("ValidationOctopusUrl"));
        Assert.That(result.Errors, Does.Contain("ValidationOctopusApiKey"));
        await validator.DidNotReceiveWithAnyArgs().ValidateConnectivity(default);
    }

    [Test]
    public async Task LoadConfiguration_UsesInjectedConnectivityValidator_ForValidConfig()
    {
        IConfiguration validatedConfig = null;
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        validator.ValidateConnectivity(Arg.Do<IConfiguration>(config => validatedConfig = config))
            .Returns(Task.CompletedTask);
        var path = WriteTempFile(JsonConvert.SerializeObject(new
        {
            OctopusUrl = "https://octopus.example",
            ApiKey = "API-123",
            DefaultChannel = "main",
            CacheTimeoutInSeconds = 30,
            CheckForBetaReleases = true
        }));
        var provider = new JsonConfigurationProvider(TestLanguageProvider.Create(), validator, path);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Configuration.OctopusUrl, Is.EqualTo("https://octopus.example"));
        Assert.That(result.Configuration.ApiKey, Is.EqualTo("API-123"));
        Assert.That(validatedConfig, Is.Not.Null);
        Assert.That(validatedConfig.DefaultChannel, Is.EqualTo("main"));
        Assert.That(validatedConfig.CheckForBetaReleases, Is.True);
    }

    [Test]
    public async Task LoadConfiguration_ReturnsConnectivityError_WhenValidatorFails()
    {
        var validator = Substitute.For<IConfigurationConnectivityValidator>();
        validator.ValidateConnectivity(Arg.Any<IConfiguration>())
            .Returns<Task>(_ => throw new InvalidOperationException("no route"));
        var path = WriteTempFile(JsonConvert.SerializeObject(new
        {
            OctopusUrl = "https://octopus.example",
            ApiKey = "API-123"
        }));
        var provider = new JsonConfigurationProvider(TestLanguageProvider.Create(), validator, path);

        var result = await provider.LoadConfiguration();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors[0], Does.Contain("ValidationOctopusApiFailure"));
        Assert.That(result.Errors[0], Does.Contain("no route"));
    }

    private static string WriteTempFile(string content)
    {
        var path = CreateTempPath();
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "shipitsharp-config-" + Guid.NewGuid() + ".json");
    }
}
