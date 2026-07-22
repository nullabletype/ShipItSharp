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


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal abstract class BaseCommand
    {

        private const string HelpOption = "-?|-h|--help";
        private readonly Dictionary<string, CommandOption> _optionRegister;
        protected ILanguageProvider LanguageProvider;
        protected IOctopusHelper OctoHelper;


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

                var code = await Run(command);
                if (code != 0)
                {
                    if (code == -1)
                    {
                        command.ShowHelp();
                    }
                }
                return code;
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

        protected string GetStringFromUser(string optionName, string prompt, TimeSpan timeout)
        {
            var option = GetStringValueFromOption(optionName);

            if (InInteractiveMode && string.IsNullOrEmpty(option))
            {
                option = PromptForStringWithTimeout(prompt, timeout, System.Console.In, System.Console.Out);
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

        internal static string PromptForStringWithTimeout(string prompt, TimeSpan timeout, TextReader input, TextWriter output)
        {
            if (ReferenceEquals(input, System.Console.In) && !System.Console.IsInputRedirected)
            {
                return PromptConsoleForStringWithTimeout(prompt, timeout, output);
            }

            using var cancellation = new CancellationTokenSource();
            var inputTask = Task.Run(async () => await input.ReadLineAsync(cancellation.Token));
            var timer = Stopwatch.StartNew();
            var displayedSeconds = -1;

            try
            {
                while (timer.Elapsed < timeout)
                {
                    var remainingSeconds = Math.Max(1, (int)Math.Ceiling((timeout - timer.Elapsed).TotalSeconds));
                    if (remainingSeconds != displayedSeconds)
                    {
                        if (displayedSeconds != -1)
                        {
                            output.Write('\r');
                        }
                        output.Write($"{prompt} ({remainingSeconds,2}s): ");
                        displayedSeconds = remainingSeconds;
                    }

                    var remainingTime = timeout - timer.Elapsed;
                    var refreshDelay = remainingTime < TimeSpan.FromSeconds(1)
                        ? remainingTime
                        : TimeSpan.FromSeconds(1);
                    var completedTask = Task.WhenAny(inputTask, Task.Delay(refreshDelay)).GetAwaiter().GetResult();
                    if (completedTask == inputTask)
                    {
                        return inputTask.GetAwaiter().GetResult();
                    }
                }

                cancellation.Cancel();
                output.WriteLine();
                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                output.WriteLine();
                return string.Empty;
            }
        }

        private static string PromptConsoleForStringWithTimeout(string prompt, TimeSpan timeout, TextWriter output)
        {
            return PromptConsoleForStringWithTimeout(
                prompt,
                timeout,
                output,
                () => System.Console.KeyAvailable,
                () => System.Console.ReadKey(intercept: true),
                Thread.Sleep);
        }

        internal static string PromptConsoleForStringWithTimeout(
            string prompt,
            TimeSpan timeout,
            TextWriter output,
            Func<bool> keyAvailable,
            Func<ConsoleKeyInfo> readKey,
            Action<int> wait)
        {
            var input = new StringBuilder();
            var timer = Stopwatch.StartNew();
            var displayedSeconds = -1;
            var timeoutCancelled = false;
            var renderedLength = 0;

            while (timeoutCancelled || timer.Elapsed < timeout)
            {
                if (!timeoutCancelled)
                {
                    var remainingSeconds = Math.Max(1, (int)Math.Ceiling((timeout - timer.Elapsed).TotalSeconds));
                    if (remainingSeconds != displayedSeconds)
                    {
                        if (displayedSeconds != -1)
                        {
                            output.Write('\r');
                        }
                        var timedPrompt = $"{prompt} ({remainingSeconds,2}s): {input}";
                        output.Write(timedPrompt);
                        output.Flush();
                        renderedLength = timedPrompt.Length;
                        displayedSeconds = remainingSeconds;
                    }
                }

                if (!keyAvailable())
                {
                    wait(25);
                    continue;
                }

                var key = readKey();
                if (key.Key == ConsoleKey.Enter)
                {
                    output.WriteLine();
                    return input.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (input.Length > 0)
                    {
                        input.Length--;
                        output.Write("\b \b");
                    }
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    input.Append(key.KeyChar);
                    if (!timeoutCancelled)
                    {
                        timeoutCancelled = true;
                        var untimedPrompt = $"{prompt}: {input}";
                        output.Write('\r');
                        output.Write(untimedPrompt.PadRight(renderedLength));
                        output.Write('\r');
                        output.Write(untimedPrompt);
                    }
                    else
                    {
                        output.Write(key.KeyChar);
                    }
                    output.Flush();
                }
            }

            output.WriteLine();
            return string.Empty;
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

        protected async Task<Machine> FetchMachineFromUserInput(string machineName, Core.Deployment.Models.Environment environment)
        {
            if (string.IsNullOrWhiteSpace(machineName))
            {
                return null;
            }

            var machine = await OctoHelper.Machines.GetMachine(machineName, environment.Id);
            if (machine == null)
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "NoMatchingMachine"));
            }

            return machine;
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
        }
    }
}
