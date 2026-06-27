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

using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class PrioritiseTask(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IProgressBar progressBar, TaskRunner runner)
        : BaseCommand(octopusHelper, languageProvider)
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "prioritise";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "TaskPrioritise");

            AddToRegister(TaskOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(TaskOptionNames.Environment, string.Empty);
            var environment = await FetchEnvironmentFromUserInput(environmentName);
            if (environment == null)
            {
                return -1;
            }

            var result = await runner.PrioritiseQueuedTasks(environment.Id, progressBar, CreateMessages("PrioritisingTask"));
            return WriteResult(environment.Name, result, "PrioritisedQueuedTasks");
        }

        private TaskRunnerMessages CreateMessages(string processingKey)
        {
            return new TaskRunnerMessages
            {
                LoadingQueuedTasks = LanguageProvider.GetString(LanguageSection.UiStrings, "LoadingQueuedTasks"),
                LoadingDeployments = LanguageProvider.GetString(LanguageSection.UiStrings, "LoadingDeployments"),
                ProcessingTask = LanguageProvider.GetString(LanguageSection.UiStrings, processingKey)
            };
        }

        private int WriteResult(string environment, TaskOperationResult result, string successKey)
        {
            if (!result.Found)
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), environment);
                return -1;
            }

            if (result.AffectedTaskIds.Count == 0)
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "NoQueuedDeploymentTasksForEnvironment"), environment);
                return 0;
            }

            System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, successKey), result.AffectedTaskIds.Count, environment);
            var table = new ConsoleTable(LanguageProvider.GetString(LanguageSection.UiStrings, "TaskId"));
            foreach (var taskId in result.AffectedTaskIds)
            {
                table.AddRow(taskId);
            }

            table.Write(Format.Minimal);
            return 0;
        }

        private struct TaskOptionNames
        {
            public const string Environment = "environment";
        }
    }
}
