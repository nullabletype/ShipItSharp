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
using System.IO;
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models.Variables;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Core.JobRunners
{
    public class VariablesWithProfileRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public VariablesWithProfileRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(string file)
        {
            var config = StandardSerialiser.DeserializeFromJsonNet<VariableSetCollection>(File.ReadAllText(file));

            if (config == null)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "FailedParsingVariableFile"));
                return -1;
            }

            foreach (var varSet in config.VariableSets)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "UpdatingVariableSet"), varSet.Id, varSet.Variables.Count);
                try
                {
                    await _octopusHelper.Variables.UpdateVariableSet(varSet);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "FailedUpdatingVariableSet"), e.Message);
                    return -1;
                }
            }

            System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Done"), string.Empty);
            return 0;
        }
    }
}
