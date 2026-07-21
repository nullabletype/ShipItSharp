using NUnit.Framework;
using ShipItSharp.Console.ConsoleTools;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class ConsoleDeploymentQueueInteractionTests
{
    [Test]
    public void ProcessKey_RecognisesQueueJumpShortcut()
    {
        var interaction = new ConsoleDeploymentQueueInteraction();

        var afterQ = interaction.ProcessKey('q');
        var afterJ = interaction.ProcessKey('j');

        Assert.That(afterQ, Is.False);
        Assert.That(afterJ, Is.True);
    }

    [Test]
    public void ProcessKey_RecognisesUppercaseQueueJumpShortcut()
    {
        var interaction = new ConsoleDeploymentQueueInteraction();

        interaction.ProcessKey('Q');

        Assert.That(interaction.ProcessKey('J'), Is.True);
    }

    [Test]
    public void ProcessKey_ResetsShortcutAfterAnUnrelatedKey()
    {
        var interaction = new ConsoleDeploymentQueueInteraction();

        interaction.ProcessKey('q');
        interaction.ProcessKey('x');

        Assert.That(interaction.ProcessKey('j'), Is.False);
    }

    [Test]
    public void Reset_ClearsPartialShortcutBetweenDeploymentJobs()
    {
        var interaction = new ConsoleDeploymentQueueInteraction();
        interaction.ProcessKey('q');

        interaction.Reset();

        Assert.That(interaction.ProcessKey('j'), Is.False);
    }
}
