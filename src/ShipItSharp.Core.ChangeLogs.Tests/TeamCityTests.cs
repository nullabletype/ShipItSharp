using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.ChangeLogs.Interfaces;
using ShipItSharp.Core.ChangeLogs.TeamCity;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Utilities;
using TeamCityProject = ShipItSharp.Core.ChangeLogs.TeamCity.Project;
using TeamCityProvider = ShipItSharp.Core.ChangeLogs.TeamCity.TeamCity;

namespace ShipItSharp.Core.ChangeLogs.Tests;

[TestFixture]
public class TeamCityTests
{
    [Test]
    public void CanProvideChangeTracking_ReturnsTrue_WhenBuildIdRegexMatches()
    {
        var teamCity = CreateTeamCity(new FakeWebRequestHelper());
        var package = Substitute.For<IVersionedPackage>();
        package.Id.Returns("Package-1");
        package.Message.Returns("Build 12345 completed");

        Assert.That(teamCity.CanProvideChangeTracking(package), Is.True);
    }

    [Test]
    public void CanProvideChangeTracking_ReturnsFalse_WhenBuildIdRegexDoesNotMatch()
    {
        var teamCity = CreateTeamCity(new FakeWebRequestHelper());
        var package = Substitute.For<IVersionedPackage>();
        package.Id.Returns("Package-1");
        package.Message.Returns("No build here");

        Assert.That(teamCity.CanProvideChangeTracking(package), Is.False);
    }

    [Test]
    public void GetChanges_ToPackage_FetchesBuildChangeListAndIssueLinks()
    {
        var helper = new FakeWebRequestHelper()
            .When<ChangeBuilds>("locator=id:123", new ChangeBuilds
            {
                Builds = new List<ChangeBuild> { new() { Id = "123", BuildTypeId = "BuildType-A" } }
            })
            .When<ChangeList>("locator=build:(id:123)", new ChangeList
            {
                Changes = new List<Change> { new() { Id = 77, Href = "/app/rest/changes/id:77" } }
            })
            .When<ChangeList>("sinceChange:(id:27)", new ChangeList
            {
                Changes = new List<Change>
                {
                    new() { Id = 70, Href = "/app/rest/changes/id:70" },
                    new() { Id = 77, Href = "/app/rest/changes/id:77" },
                    new() { Id = 99, Href = "/app/rest/changes/id:99" }
                }
            })
            .When<Change>("id:70", CreateChange(70, "PAY-70 fixed"))
            .When<Change>("id:77", CreateChange(77, "PAY-77 shipped"))
            .When<Change>("id:99", CreateChange(99, "PAY-99 should not be included"));
        var teamCity = CreateTeamCity(helper);
        var package = CreatePackage("Build 123");
        var project = Substitute.For<IVersionedProject>();
        project.ProjectId.Returns("Projects-1");
        project.ProjectName.Returns("Payments");

        var result = teamCity.GetChanges(package, project);

        Assert.That(result.Project, Is.SameAs(project));
        Assert.That(result.Changes.Select(c => c.Id), Is.EqualTo(new[] { "70", "77" }));
        Assert.That(result.Changes[0].Issues.Single().WebUrl, Is.EqualTo("https://tracker.example/PAY-70"));
    }

    [Test]
    public void GetChanges_FromAndToPackage_FetchesTargetBuildRatherThanSourceTwice()
    {
        var helper = new FakeWebRequestHelper()
            .When<ChangeBuilds>("locator=id:100", new ChangeBuilds
            {
                Builds = new List<ChangeBuild> { new() { Id = "100", BuildTypeId = "BuildType-A" } }
            })
            .When<ChangeBuilds>("locator=id:200", new ChangeBuilds
            {
                Builds = new List<ChangeBuild> { new() { Id = "200", BuildTypeId = "BuildType-A" } }
            })
            .When<ChangeList>("locator=build:(id:100)", new ChangeList
            {
                Changes = new List<Change> { new() { Id = 10, Href = "/app/rest/changes/id:10" } }
            })
            .When<ChangeList>("locator=build:(id:200)", new ChangeList
            {
                Changes = new List<Change> { new() { Id = 20, Href = "/app/rest/changes/id:20" } }
            })
            .When<ChangeList>("sinceChange:(id:10)", new ChangeList
            {
                Changes = new List<Change> { new() { Id = 20, Href = "/app/rest/changes/id:20" } }
            })
            .When<Change>("id:20", CreateChange(20, "PAY-20 shipped"));
        var teamCity = CreateTeamCity(helper);

        var result = teamCity.GetChanges(CreatePackage("Build 100"), CreatePackage("Build 200"), Substitute.For<IVersionedProject>());

        Assert.That(result.Changes.Select(c => c.Id), Is.EqualTo(new[] { "20" }));
        Assert.That(helper.RequestedUrls.Count(url => url.Contains("locator=id:200")), Is.EqualTo(1));
    }

    [Test]
    public void GetProjectStatusList_FiltersParentProjectsAndMapsStatuses()
    {
        var helper = new FakeWebRequestHelper()
            .When<ProjectList>("/app/rest/projects", new ProjectList
            {
                Projects = new List<TeamCityProject>
                {
                    new() { Id = "Root", Name = "Root", WebUrl = "https://tc/root" },
                    new() { Id = "Child", Name = "Child", ParentProjectId = "Root" }
                }
            })
            .When<BuildTypes>("affectedProject:(id:Root)", new BuildTypes { Builds = new List<BuildType>() })
            .When<BuildTypes>("affectedProject:(id:Child)", new BuildTypes
            {
                Builds = new List<BuildType>
                {
                    new()
                    {
                        Id = "BuildType-A",
                        Name = "Deploy",
                        Builds = new List<BuildConfigs>
                        {
                            new()
                            {
                                Builds = new List<Build>
                                {
                                    new() { Number = 42, Status = "SUCCESS", Statustext = "OK", WebUrl = "https://tc/build/42" },
                                    new() { Number = 43, Status = "", Statustext = "Filtered" }
                                }
                            }
                        }
                    }
                }
            });
        var teamCity = CreateTeamCity(helper);

        var projects = teamCity.GetProjectStatusList(BuildStatus.Success, 10, 5).ToList();

        Assert.That(projects, Has.Count.EqualTo(1));
        Assert.That(projects[0].Id, Is.EqualTo("Child"));
        Assert.That(projects[0].Builds.Single().Status, Is.EqualTo(BuildStatus.Success));
    }

    private static TeamCityProvider CreateTeamCity(IWebRequestHelper helper)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.ChangeProviderConfiguration.Returns(new ChangeLogProviderConfiguration
        {
            BaseUrl = "https://teamcity.example",
            Username = "user",
            Password = "pass",
            BuildIdFormat = "Build (?<buildid>\\d+)",
            IssueFormat = "(?<issueid>PAY-\\d+)",
            IssueReplacementUrl = "https://tracker.example/{issueid}"
        });
        return new TeamCityProvider(configuration, helper);
    }

    private static IVersionedPackage CreatePackage(string message)
    {
        var package = Substitute.For<IVersionedPackage>();
        package.Id.Returns("Package-1");
        package.Message.Returns(message);
        return package;
    }

    private static Change CreateChange(long id, string comment)
    {
        return new Change
        {
            Id = id,
            Version = "abcdef",
            Username = "steven",
            Date = "20260620T120000+0000",
            WebUrl = "https://teamcity.example/change/" + id,
            Comment = comment
        };
    }

    private sealed class FakeWebRequestHelper : IWebRequestHelper
    {
        private readonly List<(Type Type, string UrlContains, object Response)> _responses = new();
        public List<string> RequestedUrls { get; } = new();

        public FakeWebRequestHelper When<T>(string urlContains, T response)
        {
            _responses.Add((typeof(T), urlContains, response));
            return this;
        }

        public T GetXmlWebRequestWithBasicAuth<T>(string url, string username, string password)
        {
            RequestedUrls.Add(url);
            var match = _responses.LastOrDefault(response =>
                response.Type == typeof(T) && url.Contains(response.UrlContains, StringComparison.OrdinalIgnoreCase));
            if (match.Response == null)
            {
                throw new InvalidOperationException($"No fake response registered for {typeof(T).Name}: {url}");
            }

            return (T)match.Response;
        }
    }
}
