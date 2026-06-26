using System.Linq;
using NUnit.Framework;
using Octopus.Client.Model;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus.Tests;

[TestFixture]
public class PackageRepositoryTests
{
    [Test]
    public void GetPackageReferences_IncludesLegacyPackagesFromExternalFeeds()
    {
        var step = new DeploymentStepResource { Id = "Steps-1", Name = "Deploy web" };
        var action = new DeploymentActionResource { Name = "Deploy package" };
        action.Properties["Octopus.Action.Package.PackageId"] = new PropertyValueResource("Acme.Web");
        action.Properties["Octopus.Action.Package.FeedId"] = new PropertyValueResource("Feeds-External");

        var result = PackageRepository.GetPackageReferences(step, action).Single();

        Assert.That(result.PackageId, Is.EqualTo("Acme.Web"));
        Assert.That(result.FeedId, Is.EqualTo("Feeds-External"));
        Assert.That(result.StepName, Is.EqualTo("Deploy web"));
        Assert.That(result.StepId, Is.EqualTo("Steps-1"));
        Assert.That(result.ActionName, Is.EqualTo("Deploy package"));
    }

    [Test]
    public void GetPackageReferences_DefaultsLegacyPackagesToBuiltInFeed_WhenFeedIsMissing()
    {
        var step = new DeploymentStepResource { Id = "Steps-1", Name = "Deploy web" };
        var action = new DeploymentActionResource { Name = "Deploy package" };
        action.Properties["Octopus.Action.Package.PackageId"] = new PropertyValueResource("Acme.Web");

        var result = PackageRepository.GetPackageReferences(step, action).Single();

        Assert.That(result.FeedId, Is.EqualTo("feeds-builtin"));
    }

    [Test]
    public void GetPackageReferences_IncludesNamedPackageReferences()
    {
        var step = new DeploymentStepResource { Id = "Steps-1", Name = "Deploy web" };
        var action = new DeploymentActionResource { Name = "Deploy package" };
        action.Packages.Add(new PackageReference("Web", "Acme.Web", "Feeds-External", PackageAcquisitionLocationResource.Server));
        action.Packages.Add(new PackageReference("Tools", "Acme.Tools", "feeds-builtin", PackageAcquisitionLocationResource.Server));

        var result = PackageRepository.GetPackageReferences(step, action);

        Assert.That(result.Select(package => package.PackageId), Is.EqualTo(new[] { "Acme.Web", "Acme.Tools" }));
        Assert.That(result.Select(package => package.FeedId), Is.EqualTo(new[] { "Feeds-External", "feeds-builtin" }));
        Assert.That(result.Select(package => package.PackageReferenceName), Is.EqualTo(new[] { "Web", "Tools" }));
    }
}
