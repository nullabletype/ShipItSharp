using System.Collections.Generic;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.Models.Tests;

[TestFixture]
public class ModelBehaviourTests
{
    [Test]
    public void PackageStep_SelectedPackage_ReturnsFirstAvailablePackage()
    {
        var first = new PackageStub { Id = "Package-1", Version = "1.0.0" };
        var second = new PackageStub { Id = "Package-2", Version = "2.0.0" };
        var step = new PackageStep { AvailablePackages = new List<PackageStub> { first, second } };

        Assert.That(step.SelectedPackage, Is.SameAs(first));
    }

    [Test]
    public void PackageStep_SelectedPackage_ReturnsNull_WhenNoPackagesAreAvailable()
    {
        Assert.That(new PackageStep().SelectedPackage, Is.Null);
        Assert.That(new PackageStep { AvailablePackages = new List<PackageStub>() }.SelectedPackage, Is.Null);
    }

    [Test]
    public void Project_Checked_RaisesPropertyChanged()
    {
        var project = new Project();
        var changedProperty = string.Empty;
        project.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        project.Checked = true;

        Assert.That(changedProperty, Is.EqualTo(nameof(Project.Checked)));
    }

    [Test]
    public void Project_SelectedPackageStubs_ReflectsPackageStepSelections()
    {
        var selected = new PackageStub { Id = "Package-1", Version = "1.0.0" };
        var project = new Project
        {
            AvailablePackages = new List<PackageStep>
            {
                new() { AvailablePackages = new List<PackageStub> { selected } },
                new() { AvailablePackages = new List<PackageStub>() }
            }
        };

        Assert.That(project.SelectedPackageStubs, Is.EqualTo(new[] { selected, null }));
    }

    [Test]
    public void EnvironmentDeployment_SetPriority_UpdatesPriorityFlag()
    {
        var deployment = new EnvironmentDeployment();

        deployment.SetPriority(true);

        Assert.That(deployment.Prioritise, Is.True);
    }
}
