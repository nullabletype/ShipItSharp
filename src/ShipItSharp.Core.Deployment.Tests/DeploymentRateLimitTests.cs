using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Deployment.Tests;

public class DeploymentRateLimitTests
{
    [TestCase(false, TestName = "StartJob_HonoursRetryAfterForOnlyTheThrottledTaskAndReportsBothProjects")]
    [TestCase(true, TestName = "StartJob_ReportsUnconfirmedProjectAndItsHealthySiblingWhenRateLimitRetriesAreExhausted")]
    public async Task StartJob_ReportsEveryProjectWhenOneStatusReadIsThrottled(bool persistent)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var server = builder.Build();
        server.MapGet("/api", () => new
        {
            Version = "2026.3.1",
            ApiVersion = "3.0.0",
            Links = new { Tasks = "/api/tasks{/id}" }
        });

        var attempts = new ConcurrentQueue<(string TaskId, long Timestamp)>();
        var retryAfterSeconds = persistent ? 0 : 3;
        server.MapGet("/api/tasks/{id}", (string id, HttpResponse response) =>
        {
            attempts.Enqueue((id, Stopwatch.GetTimestamp()));
            if (id == "ServerTasks-1" && (persistent || attempts.Count(a => a.TaskId == id) == 1))
            {
                response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }
            return Results.Json(new { Id = id, State = "Success", Links = new { } });
        });
        await server.StartAsync();

        // Only status reads go over HTTP. No Octopus project, release or deployment is created.
        var octopus = await OctopusHelper.CreateAsync(server.Urls.Single(), "API-fixture");
        var helper = Substitute.For<IOctopusHelper>();
        var payments = new ProjectDeployment { ProjectName = "Payments", ReleaseId = "Releases-1" };
        var orders = new ProjectDeployment { ProjectName = "Orders", ReleaseId = "Releases-2" };
        helper.Releases.GetRelease(Arg.Any<string>()).Returns(call => new Release { Id = call.Arg<string>(), Version = "1" });
        helper.Deployments.CreateDeploymentTask(payments, "Environments-1", payments.ReleaseId, false, null)
            .Returns(new Models.Deployment { TaskId = "ServerTasks-1" });
        helper.Deployments.CreateDeploymentTask(orders, "Environments-1", orders.ReleaseId, false, null)
            .Returns(new Models.Deployment { TaskId = "ServerTasks-2" });
        helper.Deployments.GetTaskStatus(Arg.Any<string>()).Returns(call => octopus.Deployments.GetTaskStatus(call.Arg<string>()));
        var logger = Substitute.For<IUiLogger>();
        var deployer = new Deployer(helper, Substitute.For<IConfiguration>(), new LanguageProvider(), Substitute.For<IDeploymentQueueInteraction>());
        var job = new EnvironmentDeployment
        {
            EnvironmentId = "Environments-1", EnvironmentName = "Prod", DeployAsync = true,
            ProjectDeployments = new List<ProjectDeployment> { payments, orders }
        };

        await deployer.StartJob(job, logger).WaitAsync(TimeSpan.FromSeconds(20));

        var retried = attempts.Where(a => a.TaskId == "ServerTasks-1").ToArray();
        Assert.That(attempts.Count(a => a.TaskId == "ServerTasks-2"), Is.EqualTo(1));
        logger.Received(1).WriteLine(Arg.Is<string>(line => line.Contains("Orders deployed to Prod")));
        if (persistent)
        {
            Assert.That(retried.Length, Is.GreaterThan(1));
            logger.Received(1).WriteLine(Arg.Is<string>(line =>
                line.Contains("Payments") && line.Contains("ServerTasks-1") && line.Contains("could not confirm")));
            logger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Unconfirmed: 1")));
            logger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Completed: 1")));
        }
        else
        {
            Assert.That(retried, Has.Length.EqualTo(2));
            Assert.That(Stopwatch.GetElapsedTime(retried[0].Timestamp, retried[1].Timestamp),
                Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(retryAfterSeconds)));
            logger.Received(1).WriteLine(Arg.Is<string>(line => line.Contains("Payments deployed to Prod")));
            logger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Completed: 2")));
        }
        logger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Total: 2")));
        logger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Failed: 0")));
    }
}
