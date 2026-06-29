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
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentEnvironment = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Core.JobRunners
{
    public class DisableEnvironmentRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public DisableEnvironmentRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(DeploymentEnvironment environment, Machine machine = null)
        {
            if (environment == null)
            {
                return -1;
            }

            try
            {
                if (machine == null)
                {
                    await _octopusHelper.Machines.DisableMachines(environment.Id);
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentDisabled"), environment.Name);
                }
                else
                {
                    var disabled = await _octopusHelper.Machines.DisableMachine(machine.Id, environment.Id);
                    if (!disabled)
                    {
                        System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NoMatchingMachine"));
                        return -1;
                    }

                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "MachineDisabled"), machine.Name, environment.Name);
                }
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
