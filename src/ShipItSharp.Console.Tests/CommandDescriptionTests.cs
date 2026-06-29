using System;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using NUnit.Framework;
using ShipItSharp.Console.Commands;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class CommandDescriptionTests
{
    [Test]
    public void Configure_AllCommands_IncludeDescriptionsForHelp()
    {
        var commandTypes = typeof(BaseCommand).Assembly
            .GetTypes()
            .Where(type => typeof(BaseCommand).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .OrderBy(type => type.FullName)
            .ToArray();

        foreach (var commandType in commandTypes)
        {
            var command = (BaseCommand) Create(commandType);
            var app = new CommandLineApplication();

            command.Configure(app);

            Assert.That(app.Description, Is.Not.Null.And.Not.Empty, $"{commandType.FullName} must set command.Description.");
        }
    }

    [Test]
    public void Configure_AllCommands_DoNotDeclareDuplicateShortOptions()
    {
        var commandTypes = typeof(BaseCommand).Assembly
            .GetTypes()
            .Where(type => typeof(BaseCommand).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .OrderBy(type => type.FullName)
            .ToArray();

        foreach (var commandType in commandTypes)
        {
            var command = (BaseCommand) Create(commandType);
            var app = new CommandLineApplication();

            command.Configure(app);

            var duplicateShortOptions = app.GetOptions()
                .Where(option => !string.IsNullOrWhiteSpace(option.ShortName))
                .GroupBy(option => option.ShortName)
                .Where(group => group.Count() > 1)
                .Select(group => $"-{group.Key}")
                .ToArray();

            Assert.That(duplicateShortOptions, Is.Empty, $"{commandType.FullName} declares duplicate short options.");
        }
    }

    private static object Create(Type type)
    {
        var languageProvider = TestLanguageProvider.Create();
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(c => c.GetParameters().Count(p => typeof(BaseCommand).IsAssignableFrom(p.ParameterType)))
            .ThenByDescending(c => c.GetParameters().Length)
            .First();

        var arguments = constructor.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(ILanguageProvider))
            {
                return languageProvider;
            }

            if (typeof(BaseCommand).IsAssignableFrom(parameter.ParameterType))
            {
                return Create(parameter.ParameterType);
            }

            return null;
        }).ToArray();

        return constructor.Invoke(arguments);
    }
}
