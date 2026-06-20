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
using System.Threading.Tasks;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class DeleteEnvironmentRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public DeleteEnvironmentRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(string id, bool skipConfirmation, ICommandInteraction interaction)
        {
            var found = await _octopusHelper.Environments.GetEnvironment(id);
            if (found == null)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), id);
                return -1;
            }

            System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentFound"), id);
            if (!skipConfirmation && !interaction.Confirm(string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "ConfirmationCheck"), found.Name), false))
            {
                return 0;
            }

            try
            {
                await _octopusHelper.Teams.RemoveEnvironmentsFromTeams(found.Id);
                await _octopusHelper.LifeCycles.RemoveEnvironmentsFromLifecycles(found.Id);
                await _octopusHelper.Environments.DeleteEnvironment(found.Id);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + e.Message);
                return -1;
            }

            System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Done"), string.Empty);
            return 0;
        }
    }
}
