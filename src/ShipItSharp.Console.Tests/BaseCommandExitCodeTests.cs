using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Console.Commands;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class BaseCommandExitCodeTests
{
    [Test]
    public void Execute_PropagatesRunExitCode()
    {
        var app = new CommandLineApplication();
        var command = new FixedCodeCommand(Substitute.For<IOctopusHelper>(), TestLanguageProvider.Create(), -2);

        app.Command("test", command.Configure);

        var result = app.Execute("test");

        Assert.That(result, Is.EqualTo(-2));
    }

    private sealed class FixedCodeCommand : BaseCommand
    {
        private readonly int _exitCode;

        public FixedCodeCommand(IOctopusHelper octoHelper, ILanguageProvider languageProvider, int exitCode)
            : base(octoHelper, languageProvider)
        {
            _exitCode = exitCode;
        }

        protected override bool SupportsInteractiveMode => false;

        public override string CommandName => "test";

        protected override Task<int> Run(CommandLineApplication command)
        {
            return Task.FromResult(_exitCode);
        }
    }
}
