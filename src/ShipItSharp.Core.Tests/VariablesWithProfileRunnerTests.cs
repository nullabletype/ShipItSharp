using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models.Variables;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class VariablesWithProfileRunnerTests
{
    [Test]
    public async Task Run_DoesNotUpdateVariables_WhenConfirmationIsRejected()
    {
        var (runner, variables, interaction, path) = CreateRunnerAndProfile();
        try
        {
            interaction.Confirm(Arg.Any<string>(), false).Returns(false);

            var result = await runner.Run(path, false, interaction);

            Assert.That(result, Is.EqualTo(0));
            interaction.Received(1).Confirm(Arg.Any<string>(), false);
            await variables.DidNotReceive().UpdateVariableSet(Arg.Any<VariableSet>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Run_UpdatesVariablesWithoutReadingInput_WhenNoPromptIsSpecified()
    {
        var (runner, variables, interaction, path) = CreateRunnerAndProfile();
        try
        {
            var result = await runner.Run(path, true, interaction);

            Assert.That(result, Is.EqualTo(0));
            interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
            await variables.Received(1).UpdateVariableSet(Arg.Is<VariableSet>(set => set.Id == "variables-1"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (VariablesWithProfileRunner Runner, IVariableRepository Variables, ICommandInteraction Interaction, string Path) CreateRunnerAndProfile()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var variables = Substitute.For<IVariableRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Variables.Returns(variables);
        var profile = new VariableSetCollection();
        profile.VariableSets.Add(new VariableSet { Id = "variables-1" });
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"variables-{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, StandardSerialiser.SerializeToJsonNet(profile));
        return (new VariablesWithProfileRunner(helper, TestLanguageProvider.Create()), variables, interaction, path);
    }
}
