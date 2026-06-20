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

using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class EnsureEnvironmentRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public EnsureEnvironmentRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(string name, string description)
        {
            var found = await _octopusHelper.Environments.GetMatchingEnvironments(name);
            Deployment.Models.Environment environment;
            if (found.Any())
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentFound"), name);
                environment = found.First();
            }
            else
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), name);
                environment = await _octopusHelper.Environments.CreateEnvironment(name, description);
            }

            System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "EnvionmentId"), environment.Id);
            return 0;
        }
    }
}
