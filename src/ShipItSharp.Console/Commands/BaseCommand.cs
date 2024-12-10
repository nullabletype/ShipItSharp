#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
// 
//     Copyright (C) 2022  Steven Davies
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging;
using ShipItSharp.Core.Logging.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal abstract class BaseCommand
    {

        private const string HelpOption = "-?|-h|--help";
        private readonly Dictionary<string, CommandOption> _optionRegister;
        protected ILanguageProvider LanguageProvider;
        protected IOctopusHelper OctoHelper;
        protected IShipItLogger Log;

        protected BaseCommand(IOctopusHelper octoHelper, ILanguageProvider languageProvider)
        {
            _optionRegister = new Dictionary<string, CommandOption>();
            OctoHelper = octoHelper;
            LanguageProvider = languageProvider;
        }
        protected abstract bool SupportsInteractiveMode { get; }
        public abstract string CommandName { get; }
        protected bool InInteractiveMode { get; private set; }
        protected abstract Task<int> Run(CommandLineApplication command);

        public virtual void Configure(CommandLineApplication command)
        {
            command.HelpOption(HelpOption);
            command.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw;
            AddToRegister(OptionNames.ApiKey, command.Option("-a|--apikey", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ApiKey"), CommandOptionType.SingleValue));
            AddToRegister(OptionNames.Url, command.Option("-u|--url", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Url"), CommandOptionType.SingleValue));
            AddToRegister(OptionNames.Verbose, command.Option("-v|--verbose", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Verbose"), CommandOptionType.NoValue));
            if (SupportsInteractiveMode)
            {
                AddToRegister(OptionNames.NoPrompt, command.Option("-n|--noprompt", LanguageProvider.GetString(LanguageSection.OptionsStrings, "InteractiveDeploy"), CommandOptionType.NoValue));
            }
            command.OnExecuteAsync(async _ =>
            {
                if (SupportsInteractiveMode && !GetOption(OptionNames.NoPrompt).HasValue())
                {
                    SetInteractiveMode(true);
                }
                
                var method = typeof(LoggingProvider)
                    .GetMethod("GetLogger")
                    .MakeGenericMethod(GetType());
                Log = method.Invoke(null, null) as IShipItLogger;

                var code = await Run(command);
                if (code != 0)
                {
                    if (code == -1)
                    {
                        command.ShowHelp();
                    }
                }
            });
        }

        protected static void ConfigureSubCommand(BaseCommand child, CommandLineApplication command)
        {
            command.Command(child.CommandName, child.Configure);
        }

        protected void SetInteractiveMode(bool mode)
        {
            InInteractiveMode = mode;
        }

        protected void AddToRegister(string key, CommandOption option)
        {
            _optionRegister.Add(key, option);
        }

        protected CommandOption GetOption(string key)
        {
            return _optionRegister[key];
        }

        protected string GetStringValueFromOption(string key)
        {
            var option = GetOption(key);
            if (option.HasValue())
            {
                return option.Value();
            }
            return string.Empty;
        }

        public bool GetBoolValueFromOption(string key)
        {
            var option = GetOption(key);
            if (option.HasValue())
            {
                return option.HasValue();
            }
            return false;
        }

        public bool TryGetIntValueFromOption(string key, out int value)
        {
            var option = GetOption(key);
            value = 0;
            if (option.HasValue())
            {
                return int.TryParse(option.Value(), out value);
            }
            return false;
        }

        protected string GetStringFromUser(string optionName, string prompt, bool allowEmpty = false)
        {
            var option = GetStringValueFromOption(optionName);

            if (InInteractiveMode)
            {
                if (allowEmpty)
                {
                    if (string.IsNullOrEmpty(option))
                    {
                        option = Prompt.GetString(prompt);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(option))
                    {
                        option = PromptForStringWithoutQuitting(prompt);
                    }
                }
            }

            return option;
        }

        protected static string PromptForStringWithoutQuitting(string prompt)
        {
            string channel;
            do
            {
                channel = Prompt.GetString(prompt);
            } while (string.IsNullOrEmpty(channel));

            return channel;
        }

        protected string PromptForReleaseName()
        {
            string releaseName;

            do
            {
                releaseName = GetStringFromUser(OptionNames.ReleaseName, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"), true);
            } while (InInteractiveMode && !string.IsNullOrEmpty(releaseName) && !SemanticVersion.TryParse(releaseName, out _));

            return releaseName;
        }

        protected async Task<bool> ValidateDeployment(EnvironmentDeployment deployment, IDeployer deployer)
        {
            if (deployment == null)
            {
                return true;
            }

            var result = await deployer.CheckDeployment(deployment);
            if (result.Success)
            {
                return true;
            }
            System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);

            return false;
        }

        protected async Task<Core.Deployment.Models.Environment> FetchEnvironmentFromUserInput(string environmentName)
        {
            var matchingEnvironments = await OctoHelper.Environments.GetMatchingEnvironments(environmentName);

            if (matchingEnvironments.Count > 1)
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "TooManyMatchingEnvironments") + string.Join(", ", matchingEnvironments.Select(e => e.Name)));
                return null;
            }
            if (!matchingEnvironments.Any())
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "NoMatchingEnvironments"));
                return null;
            }

            return matchingEnvironments.First();
        }

        protected void FillRequiredVariables(List<ProjectDeployment> projects)
        {
            foreach (var project in projects)
            {
                if (project.RequiredVariables != null)
                {
                    foreach (var requirement in project.RequiredVariables)
                    {
                        do
                        {
                            var prompt = string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "VariablePrompt"), requirement.Name, project.ProjectName);
                            if (!string.IsNullOrEmpty(requirement.ExtraOptions))
                            {
                                prompt += string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "VariablePromptAllowedValues"), requirement.ExtraOptions);
                            }
                            requirement.Value = PromptForStringWithoutQuitting(prompt);
                        } while (InInteractiveMode && string.IsNullOrEmpty(requirement.Value));
                    }

                }
            }
        }

        public struct OptionNames
        {
            public const string NoPrompt = "noprompt";
            public const string ApiKey = "apikey";
            public const string Url = "url";
            public const string ReleaseName = "ReleaseName";
            public const string Verbose = "Verbose";
        }
    }
}