using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class ShowEnvironmentRunnerTests
{
    [Test]
    public async Task Run_ReturnsRows_WhenEnvironmentExists()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var projects = Substitute.For<IProjectRepository>();
        var releases = Substitute.For<IReleaseRepository>();
        var progressBar = Substitute.For<IProgressBar>();

        helper.Environments.Returns(environments);
        helper.Projects.Returns(projects);
        helper.Releases.Returns(releases);

        environments.GetEnvironment("Environments-1").Returns(Task.FromResult(new Environment { Id = "Environments-1", Name = "Test" }));
        projects.GetProjectStubs().Returns(Task.FromResult(new List<ProjectStub>
        {
            new() { ProjectId = "Projects-1", ProjectName = "Payments", ProjectGroupId = "Groups-1" }
        }));
        releases.GetReleasedVersion("Projects-1", "Environments-1")
            .Returns(Task.FromResult((new Release { Version = "1.2.3", DisplayPackageVersion = "pkg-1.2.3", LastModifiedBy = "steven" }, new Deployment())));

        var runner = new ShowEnvironmentRunner(helper);

        var result = await runner.Run("Environments-1", null, progressBar, "fetch", "groups", "loading {0}");

        Assert.That(result.Found, Is.True);
        Assert.That(result.Rows.Count, Is.EqualTo(1));
        Assert.That(result.Rows[0].ProjectName, Is.EqualTo("Payments"));
        Assert.That(result.Rows[0].ReleaseName, Is.EqualTo("1.2.3"));
    }

    [Test]
    public async Task Run_ReturnsNotFound_WhenEnvironmentIsMissing()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var progressBar = Substitute.For<IProgressBar>();

        helper.Environments.Returns(environments);
        environments.GetEnvironment("missing").Returns(Task.FromResult<Environment>(null));

        var runner = new ShowEnvironmentRunner(helper);

        var result = await runner.Run("missing", null, progressBar, "fetch", "groups", "loading {0}");

        Assert.That(result.Found, Is.False);
        Assert.That(result.Rows, Is.Empty);
    }
}
