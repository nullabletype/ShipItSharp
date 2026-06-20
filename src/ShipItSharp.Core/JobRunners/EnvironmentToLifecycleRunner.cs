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
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.JobRunners
{
    public class EnvironmentToLifecycleRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public EnvironmentToLifecycleRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(string environmentId, string lifecycleId, string stringPhaseId, bool automatic)
        {
            if (string.IsNullOrEmpty(environmentId))
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NoMatchingEnvironments"));
                return -1;
            }

            if (string.IsNullOrEmpty(lifecycleId))
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "LifecycleDoesntExist"));
                return -1;
            }

            if (string.IsNullOrEmpty(stringPhaseId) || !int.TryParse(stringPhaseId, out var phaseId))
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "LifecyclePhaseIsInvalid"));
                return -1;
            }

            var result = await _octopusHelper.LifeCycles.AddEnvironmentToLifecyclePhase(environmentId, lifecycleId, phaseId - 1, automatic);
            if (result.Success)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Done"), string.Empty);
                return 0;
            }

            switch (result.ErrorType)
            {
                case LifecycleErrorType.EnvironmentNotFound:
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NoMatchingEnvironments"));
                    break;
                case LifecycleErrorType.LifeCycleNotFound:
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "LifecycleDoesntExist"));
                    break;
                case LifecycleErrorType.PhaseInLifeCycleNotFound:
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "LifecyclePhaseIsInvalid"));
                    break;
                default:
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "CouldntAddEnvToTeam"), result.Error);
                    break;
            }

            return -1;
        }
    }
}
